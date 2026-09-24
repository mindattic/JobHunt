using System.Reflection;
using System.Text.Json;
using AutoWebNav;
using JobHunt.Core.Jobs;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Boards.LinkedIn;

/// <summary>The page scripts, embedded in this assembly (Boards/LinkedIn/Scripts/*.js).</summary>
internal static class LinkedInScripts
{
    public static string Results { get; } = Load("results.js");
    public static string Scroll { get; } = Load("scroll.js");
    public static string Details { get; } = Load("details.js");
    private static string CardTargetTemplate { get; } = Load("card-target.js");

    public static string CardTarget(string jobId) => CardTargetTemplate.Replace("__JOB_ID__", JsonSerializer.Serialize(jobId));

    private static string Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"JobHunt.LinkedIn.{name}")
            ?? throw new InvalidOperationException($"Embedded LinkedIn script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Reads LinkedIn job search in the user's own signed-in browser: loads a results page, scrolls
/// the list so every lazily-drawn card renders, clicks each card with a trusted click, and reads
/// the details pane. Reading only — it never clicks Apply, Save, or anything that changes state.
/// </summary>
public sealed class LinkedInSearcher(IBrowserSurface browser, IActionPacer pacer, ILogger log,
    string baseUrl = LinkedInJobBoard.DefaultBaseUrl) : IBoardSearcher
{
    public int PageSize => 25;

    /// <summary>How long a page may take to show its first cards.</summary>
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

        // Scroll until the list stops growing or ends, so off-screen shells get their content.
        for (var step = 0; step < MaxScrollSteps; step++)
        {
            var scrolled = JsonSerializer.Deserialize<ScrollJson>(await browser.EvalAsync(LinkedInScripts.Scroll, ct), Json);
            await pacer.BriefAsync(ct);
            if (scrolled?.AtBottom == true) break;
        }
        page = await ReadResultsAsync(ct);

        var cards = (page.Cards ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .Select(c => new ListingCard(c.Id, LinkedInText.Undouble(c.Title), LinkedInText.Undouble(c.Company),
                LinkedInText.Undouble(c.Location), c.QuickApply, c.Applied, c.Promoted))
            .ToList();
        log.LogInformation("Read {Count} cards from {Url}", cards.Count, url);
        return new ResultsPage(ResultsState.Results, cards, url);
    }

    public async Task<ListingDetails?> OpenListingAsync(ListingCard card, CancellationToken ct)
    {
        // Prefer clicking the card (what a person does, and it keeps the results page); fall back to
        // the job's own page when the card can't be found on screen.
        var target = JsonSerializer.Deserialize<TargetJson>(await browser.EvalAsync(LinkedInScripts.CardTarget(card.ExternalId), ct), Json);
        if (target is { Found: true })
        {
            await browser.ClickAtPointAsync(target.X, target.Y, ct);
            var fromPane = await WaitForDetailsAsync(card.ExternalId, ct);
            if (fromPane != null) return fromPane;
        }
        log.LogDebug("Opening job {Id} on its own page", card.ExternalId);
        await browser.NavigateAsync(LinkedInText.ViewUrl(card.ExternalId, baseUrl), ct);
        return await WaitForDetailsAsync(card.ExternalId, ct);
    }

    private async Task<ListingDetails?> WaitForDetailsAsync(string jobId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + DetailsTimeout;
        while (DateTime.UtcNow <= deadline)
        {
            var d = JsonSerializer.Deserialize<DetailsJson>(await browser.EvalAsync(LinkedInScripts.Details, ct), Json);
            if (d?.State == "challenge") throw new BoardChallengeException("LinkedIn is asking for a verification step.");
            if (d?.State == "signedOut") throw new BoardSignedOutException("LinkedIn signed out.");
            // The pane must be showing THIS job — right after a click the URL already names the new
            // job while the pane still shows the last one, so the id comes from inside the pane and
            // an unknown id is never taken on trust.
            if (d is { State: "details" } && d.Id == jobId)
            {
                return new ListingDetails(jobId, LinkedInText.Undouble(d.Title), LinkedInText.Undouble(d.Company),
                    LinkedInText.Undouble(d.Location), d.Facts ?? [], d.Insights ?? [], d.Description ?? "", d.Apply ?? "none");
            }
            await pacer.BriefAsync(ct);
        }
        log.LogWarning("Details for job {Id} never appeared", jobId);
        return null;
    }

    private async Task<ResultsJson> ReadResultsAsync(CancellationToken ct) =>
        JsonSerializer.Deserialize<ResultsJson>(await browser.EvalAsync(LinkedInScripts.Results, ct), Json)
        ?? new ResultsJson("loading", null);

    public JobPosting ToPosting(ListingCard card, ListingDetails? details, DateTimeOffset now) => Map(card, details, now, baseUrl);

    /// <summary>What LinkedIn itself states about a job — the LLM refines it later.</summary>
    internal static JobPosting Map(ListingCard card, ListingDetails? details, DateTimeOffset now,
        string baseUrl = LinkedInJobBoard.DefaultBaseUrl)
    {
        var insights = details?.Insights ?? [];
        var facts = details?.TopCardFacts ?? [];
        var salaryText = insights.FirstOrDefault(LinkedInText.LooksLikeSalary) ?? "";
        var (min, max, period, currency) = LinkedInText.ParsePay(salaryText);

        return new JobPosting
        {
            BoardId = LinkedInJobBoard.BoardId,
            ExternalId = card.ExternalId,
            Url = LinkedInText.ViewUrl(card.ExternalId, baseUrl),
            Title = First(details?.Title, card.Title),
            Company = First(details?.Company, card.Company),
            Location = First(card.Location, details?.Location),
            SupportsQuickApply = details is null ? card.QuickApply : details.ApplyKind == "easy" || (details.ApplyKind == "none" && card.QuickApply),
            BoardShowsApplied = card.Applied || details?.ApplyKind == "applied",
            RawDescription = details?.Description ?? "",
            PostedAt = facts.Select(f => LinkedInText.ParsePostedAgo(f, now)).FirstOrDefault(d => d != null),
            ApplicantCount = facts.Select(LinkedInText.ParseApplicants).FirstOrDefault(n => n != null),
            Workplace = LinkedInText.ParseWorkplace(insights.Concat([card.Location])),
            EmploymentType = LinkedInText.ParseEmploymentType(insights),
            SalaryMin = min,
            SalaryMax = max,
            SalaryPeriod = period,
            SalaryText = salaryText,
            SalaryCurrency = currency.Length > 0 ? currency : "USD",
        };
    }

    private static string First(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
