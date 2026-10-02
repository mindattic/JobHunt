using AutoWebNav;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Core.Boards.Indeed;

/// <summary>
/// Indeed. Like LinkedIn, Indeed has no public job-search/application API JobHunt can use, so it
/// drives the site itself in the user's own signed-in browser.
/// <para>
/// This is the pilot for JobHunt's second board and the first built on
/// <see cref="FingerprintBoardSignIn"/> instead of a hand-rolled sign-in class — see
/// <see cref="IndeedSignInProfile"/>. Indeed's markup could not be verified against a live page
/// while writing this (no browser in that environment); URL params and the searcher's selectors
/// are a best-effort first draft expected to need a short live-testing/iteration pass.
/// </para>
/// </summary>
public sealed class IndeedJobBoard(ILoggerFactory? logs = null, string? baseUrl = null) : ISearchableBoard
{
    public const string DefaultBaseUrl = "https://www.indeed.com";

    /// <summary>The site root. JOBHUNT_INDEED_BASE_URL points it at a local stand-in for offline
    /// testing, mirroring JOBHUNT_LINKEDIN_BASE_URL.</summary>
    public string BaseUrl { get; } = (baseUrl ?? Environment.GetEnvironmentVariable("JOBHUNT_INDEED_BASE_URL") ?? DefaultBaseUrl).TrimEnd('/');

    public const string BoardId = "indeed";

    public string Id => BoardId;
    public string DisplayName => "Indeed";
    public string SignInUrl => "https://secure.indeed.com/auth";
    public string HomeUrl => $"{BaseUrl}/jobs";

    /// <summary>Indeed's account-session cookie, confirmed against a live signed-in pane
    /// (2026-09-30). Not CTK: that's a visitor-tracking cookie Indeed sets for anonymous visitors
    /// too, so it would report "signed in" while signed out.</summary>
    public string? SessionCookieName => "PPID";
    public string QuickApplyName => "Indeed Apply";

    public IBoardSearcher CreateSearcher(IBrowserSurface browser, IActionPacer pacer) =>
        new IndeedSearcher(browser, pacer, (logs ?? NullLoggerFactory.Instance).CreateLogger<IndeedSearcher>(), BaseUrl);

    public string BuildSearchUrl(string searchTerm, HuntFilters filters, int page = 0)
    {
        var q = new List<(string Key, string Value)> { ("q", searchTerm.Trim()) };

        var explicitLocation = filters.BoardOptions.GetValueOrDefault($"{BoardId}.location");
        var location = explicitLocation ?? filters.Location;
        // Only guess "Remote" when the user hasn't actually named a place — someone remote-only
        // but anchored to a city (timezone, occasional travel) keeps that city.
        var hasRealLocation = explicitLocation != null ||
            (!string.IsNullOrWhiteSpace(filters.Location) && !string.Equals(filters.Location, "United States", StringComparison.OrdinalIgnoreCase));
        if (!hasRealLocation && filters.Workplaces.Count == 1 && filters.Workplaces[0] == WorkplaceType.Remote)
            location = "Remote";
        if (!string.IsNullOrWhiteSpace(location) && !string.Equals(location, "United States", StringComparison.OrdinalIgnoreCase))
            q.Add(("l", location.Trim()));

        if (filters.DistanceMiles is > 0) q.Add(("radius", filters.DistanceMiles.Value.ToString()));

        var jobTypes = filters.EmploymentTypes.Select(JobTypeCode).Where(c => c != null).Distinct().ToList();
        if (jobTypes.Count > 0) q.Add(("jt", jobTypes[0]!)); // Indeed's jt takes one value, not a list.

        var fromAge = filters.DatePosted switch
        {
            DatePosted.PastDay => "1",
            DatePosted.PastWeek => "7",
            DatePosted.PastMonth => "30",
            _ => null,
        };
        if (fromAge != null) q.Add(("fromage", fromAge));

        if (filters.Sort == SortOrder.MostRecent) q.Add(("sort", "date"));

        if (page > 0) q.Add(("start", (page * 10).ToString()));

        return $"{BaseUrl}/jobs?" + string.Join('&', q.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    /// <summary>Indeed has no contract-to-hire type; those are listed as Contract.</summary>
    private static string? JobTypeCode(EmploymentType t) => t switch
    {
        EmploymentType.FullTime => "fulltime",
        EmploymentType.PartTime => "parttime",
        EmploymentType.Contract or EmploymentType.ContractToHire => "contract",
        EmploymentType.Temporary => "temporary",
        EmploymentType.Internship => "internship",
        _ => null,
    };
}
