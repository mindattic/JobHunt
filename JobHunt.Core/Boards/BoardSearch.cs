using AutoWebNav;
using JobHunt.Core.Jobs;

namespace JobHunt.Core.Boards;

/// <summary>What a results page turned out to be.</summary>
public enum ResultsState
{
    Results,
    /// <summary>The board says nothing matched — this term is done.</summary>
    NoResults,
    /// <summary>The board wants the user signed in first.</summary>
    SignedOut,
    /// <summary>A CAPTCHA, verification or "unusual activity" page. Stop everything; only the
    /// account holder may answer it.</summary>
    Challenge,
    /// <summary>The page never showed a recognizable results list.</summary>
    Unrecognized,
}

/// <summary>One card in a results list, as listed (before its details are opened).</summary>
public sealed record ListingCard(
    string ExternalId,
    string Title,
    string Company,
    string Location,
    bool QuickApply,
    bool Applied,
    bool Promoted);

public sealed record ResultsPage(ResultsState State, IReadOnlyList<ListingCard> Cards, string Url)
{
    public static ResultsPage Of(ResultsState state, string url) => new(state, [], url);
}

/// <summary>A job's details pane, as the board shows it.</summary>
public sealed record ListingDetails(
    string ExternalId,
    string Title,
    string Company,
    string Location,
    /// <summary>The "location · posted · applicants" line(s) under the title.</summary>
    IReadOnlyList<string> TopCardFacts,
    /// <summary>Tag/pill texts: "Remote", "Full-time", "$120K/yr - $150K/yr", "Mid-Senior level".</summary>
    IReadOnlyList<string> Insights,
    string Description,
    /// <summary>"easy", "external", "applied" or "none".</summary>
    string ApplyKind);

/// <summary>
/// Reads one board's search results and job details from a live browser. Everything site-specific
/// — URLs, markup, wording — lives in the implementation; the hunt runner only sees these calls.
/// </summary>
public interface IBoardSearcher
{
    Task<ResultsPage> OpenResultsAsync(string url, CancellationToken ct);

    /// <summary>Opens a card's details. Null when they never appeared.</summary>
    Task<ListingDetails?> OpenListingAsync(ListingCard card, CancellationToken ct);

    /// <summary>Turns what was read into a posting (listing-level facts only; the LLM adds the rest).</summary>
    JobPosting ToPosting(ListingCard card, ListingDetails? details, DateTimeOffset now);

    /// <summary>Results a full page holds; fewer means this was the last page.</summary>
    int PageSize { get; }
}

/// <summary>Creates a searcher over a live page. Implemented by boards that support searching.</summary>
public interface ISearchableBoard : IJobBoard
{
    IBoardSearcher CreateSearcher(IBrowserSurface browser, IActionPacer pacer);
}

/// <summary>Human-paced pauses between browser actions — searching at machine speed is both
/// unkind to the site and the fastest way to get an account flagged.</summary>
public interface IActionPacer
{
    /// <summary>A normal pause between actions (settings' min–max range).</summary>
    Task PauseAsync(CancellationToken ct);

    /// <summary>A short pause while waiting for a page to draw.</summary>
    Task BriefAsync(CancellationToken ct);
}

public sealed class RandomPacer(Func<(int MinMs, int MaxMs)> range) : IActionPacer
{
    public Task PauseAsync(CancellationToken ct)
    {
        var (min, max) = range();
        return Task.Delay(Random.Shared.Next(Math.Max(0, min), Math.Max(min, max) + 1), ct);
    }

    public Task BriefAsync(CancellationToken ct) => Task.Delay(Random.Shared.Next(250, 500), ct);
}

/// <summary>No pauses — for tests.</summary>
public sealed class NoPacer : IActionPacer
{
    public Task PauseAsync(CancellationToken ct) => Task.CompletedTask;
    public Task BriefAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>The board is showing a CAPTCHA / verification / security check. Stop; the account
/// holder answers it.</summary>
public sealed class BoardChallengeException(string message) : Exception(message);

/// <summary>The board signed the user out mid-run.</summary>
public sealed class BoardSignedOutException(string message) : Exception(message);
