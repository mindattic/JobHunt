using System.Reflection;
using System.Text.Json;
using AutoWebNav;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Jobs;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Boards.Indeed;

/// <summary>The page scripts, embedded in this assembly (Boards/Indeed/Scripts/*.js).</summary>
internal static class IndeedScripts
{
    public static string Results { get; } = Load("results.js");
    public static string Scroll { get; } = Load("scroll.js");
    public static string Details { get; } = Load("details.js");
    private static string CardTargetTemplate { get; } = Load("card-target.js");

    public static string CardTarget(string jobId) => CardTargetTemplate.Replace("__JOB_ID__", JsonSerializer.Serialize(jobId));

    private static string Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"JobHunt.Indeed.{name}")
            ?? throw new InvalidOperationException($"Embedded Indeed script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Reads Indeed job search in the user's own signed-in browser: loads a results page, scrolls the
/// list so every lazily-drawn card renders, clicks each card with a trusted click, and reads the
/// details pane. Reading only — it never clicks Apply, Save, or anything that changes state.
/// Mirrors <c>LinkedInSearcher</c>'s shape exactly; see <see cref="IndeedJobBoard"/>'s doc comment
/// for the best-effort-selectors caveat.
/// </summary>
public sealed class IndeedSearcher(IBrowserSurface browser, IActionPacer pacer, ILogger log,
    string baseUrl = IndeedJobBoard.DefaultBaseUrl) : IBoardSearcher
{
    public int PageSize => 15;

    public TimeSpan ResultsTimeout { get; init; } = TimeSpan.FromSeconds(25);
    public TimeSpan DetailsTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public int MaxScrollSteps { get; init; } = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record ResultsJson(string State, List<CardJson>? Cards);
    private sealed record CardJson(string Id, string Title, string Company, string Location, bool QuickApply, bool Applied, bool Promoted);
    private sealed record DetailsJson(string State, string? Id, string? Title, string? Company, string? Location,
        List<string>? Facts, List<string>? Insights, string? Description, string? Apply);
    private sealed record ScrollJson(bool AtBottom);
    private sealed record TargetJson(bool Found, double X, double Y);

    public async Task<ResultsPage> OpenResultsAsync(string url, CancellationToken ct)
    {
        await browser.NavigateAsync(url, ct);
        var deadline = DateTime.UtcNow + ResultsTimeout;
        ResultsJson page;
        while (true)
        {
            page = await ReadResultsAsync(ct);
            if (page.State is "challenge") return ResultsPage.Of(ResultsState.Challenge, url);
            if (page.State is "signedOut") return ResultsPage.Of(ResultsState.SignedOut, url);
            if (page.State is "noResults") return ResultsPage.Of(ResultsState.NoResults, url);
            if (page.State is "results") break;
            if (DateTime.UtcNow > deadline)
            {
                log.LogWarning("No results list appeared at {Url}", url);
                return ResultsPage.Of(ResultsState.Unrecognized, url);
            }
            await pacer.BriefAsync(ct);
        }

        for (var step = 0; step < MaxScrollSteps; step++)
        {
            var scrolled = JsonSerializer.Deserialize<ScrollJson>(await browser.EvalAsync(IndeedScripts.Scroll, ct), Json);
            await pacer.BriefAsync(ct);
            if (scrolled?.AtBottom == true) break;
        }
        page = await ReadResultsAsync(ct);

        var cards = (page.Cards ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .Select(c => new ListingCard(c.Id, c.Title, c.Company, c.Location, c.QuickApply, c.Applied, c.Promoted))
            .ToList();
        log.LogInformation("Read {Count} cards from {Url}", cards.Count, url);
        return new ResultsPage(ResultsState.Results, cards, url);
    }

    public async Task<ListingDetails?> OpenListingAsync(ListingCard card, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<TargetJson>(await browser.EvalAsync(IndeedScripts.CardTarget(card.ExternalId), ct), Json);
        if (target is { Found: true })
        {
            await browser.ClickAtPointAsync(target.X, target.Y, ct);
            var fromPane = await WaitForDetailsAsync(card.ExternalId, ct);
            if (fromPane != null) return fromPane;
        }
        log.LogDebug("Opening job {Id} on its own page", card.ExternalId);
        await browser.NavigateAsync($"{baseUrl}/viewjob?jk={card.ExternalId}", ct);
        return await WaitForDetailsAsync(card.ExternalId, ct);
    }

    private async Task<ListingDetails?> WaitForDetailsAsync(string jobId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + DetailsTimeout;
        while (DateTime.UtcNow <= deadline)
        {
            var d = JsonSerializer.Deserialize<DetailsJson>(await browser.EvalAsync(IndeedScripts.Details, ct), Json);
            if (d?.State == "challenge") throw new BoardChallengeException("Indeed is asking for a verification step.");
            if (d?.State == "signedOut") throw new BoardSignedOutException("Indeed signed out.");
            if (d is { State: "details" } && (d.Id == jobId || string.IsNullOrEmpty(d.Id)))
            {
                return new ListingDetails(jobId, d.Title ?? "", d.Company ?? "",
                    d.Location ?? "", d.Facts ?? [], d.Insights ?? [], d.Description ?? "", d.Apply ?? "none");
            }
            await pacer.BriefAsync(ct);
        }
        log.LogWarning("Details for job {Id} never appeared", jobId);
        return null;
    }

    private async Task<ResultsJson> ReadResultsAsync(CancellationToken ct) =>
        JsonSerializer.Deserialize<ResultsJson>(await browser.EvalAsync(IndeedScripts.Results, ct), Json)
        ?? new ResultsJson("loading", null);

    public JobPosting ToPosting(ListingCard card, ListingDetails? details, DateTimeOffset now) => Map(card, details, now, baseUrl);

    /// <summary>What Indeed itself states about a job — the LLM refines it later.</summary>
    internal static JobPosting Map(ListingCard card, ListingDetails? details, DateTimeOffset now, string baseUrl = IndeedJobBoard.DefaultBaseUrl)
    {
        var insights = details?.Insights ?? [];
        var facts = details?.TopCardFacts ?? [];
        // "$175,000 - $200,000 a year" — the same wording LinkedInText's money/period regexes read.
        var salaryText = insights.Concat(facts).FirstOrDefault(LinkedInText.LooksLikeSalary) ?? "";
        var (min, max, period, currency) = LinkedInText.ParsePay(salaryText);

        return new JobPosting
        {
            BoardId = IndeedJobBoard.BoardId,
            ExternalId = card.ExternalId,
            Url = $"{baseUrl}/viewjob?jk={card.ExternalId}",
            Title = First(details?.Title, card.Title),
            Company = First(details?.Company, card.Company),
            Location = First(card.Location, details?.Location),
            SupportsQuickApply = details is null ? card.QuickApply : details.ApplyKind == "easy" || (details.ApplyKind == "none" && card.QuickApply),
            BoardShowsApplied = card.Applied || details?.ApplyKind == "applied",
            RawDescription = details?.Description ?? "",
            Workplace = ParseWorkplace(insights.Concat(facts).Concat([card.Location])),
            EmploymentType = ParseEmploymentType(facts.Concat(insights)),
            SalaryMin = min,
            SalaryMax = max,
            SalaryPeriod = period,
            SalaryText = salaryText,
            SalaryCurrency = currency.Length > 0 ? currency : "USD",
        };
    }

    /// <summary>Every text is checked, not just the first — the first pill is usually the pay.
    /// Hybrid before remote: Indeed writes "Hybrid remote in Chicago, IL".</summary>
    internal static WorkplaceType ParseWorkplace(IEnumerable<string> texts)
    {
        var all = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (all.Any(t => t.Contains("hybrid", StringComparison.OrdinalIgnoreCase))) return WorkplaceType.Hybrid;
        if (all.Any(t => t.Contains("remote", StringComparison.OrdinalIgnoreCase))) return WorkplaceType.Remote;
        if (all.Any(t => t.Contains("in person", StringComparison.OrdinalIgnoreCase) || t.Contains("on-site", StringComparison.OrdinalIgnoreCase)))
            return WorkplaceType.OnSite;
        return WorkplaceType.Unknown;
    }

    private static EmploymentType ParseEmploymentType(IEnumerable<string> texts)
    {
        foreach (var t in texts)
        {
            if (t.Contains("full-time", StringComparison.OrdinalIgnoreCase) || t.Contains("full time", StringComparison.OrdinalIgnoreCase)) return EmploymentType.FullTime;
            if (t.Contains("part-time", StringComparison.OrdinalIgnoreCase) || t.Contains("part time", StringComparison.OrdinalIgnoreCase)) return EmploymentType.PartTime;
            if (t.Contains("contract to hire", StringComparison.OrdinalIgnoreCase)) return EmploymentType.ContractToHire;
            if (t.Contains("contract", StringComparison.OrdinalIgnoreCase)) return EmploymentType.Contract;
            if (t.Contains("temporary", StringComparison.OrdinalIgnoreCase)) return EmploymentType.Temporary;
            if (t.Contains("internship", StringComparison.OrdinalIgnoreCase)) return EmploymentType.Internship;
        }
        return EmploymentType.Unknown;
    }

    private static string First(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
