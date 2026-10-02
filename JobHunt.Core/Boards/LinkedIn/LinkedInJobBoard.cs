using AutoWebNav;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Core.Boards.LinkedIn;

/// <summary>
/// LinkedIn Jobs. LinkedIn has no public job-search or application API, so JobHunt drives the
/// site itself in the user's own signed-in browser — the same approach KdpPublish takes with KDP.
/// </summary>
public sealed class LinkedInJobBoard(ILoggerFactory? logs = null, string? baseUrl = null) : ISearchableBoard
{
    public const string DefaultBaseUrl = "https://www.linkedin.com";

    /// <summary>
    /// The site root. Always LinkedIn in use; JOBHUNT_LINKEDIN_BASE_URL points it at a local
    /// stand-in so tools/verify-hunt.mjs can drive the whole hunt through the real app offline.
    /// </summary>
    public string BaseUrl { get; } = (baseUrl ?? Environment.GetEnvironmentVariable("JOBHUNT_LINKEDIN_BASE_URL") ?? DefaultBaseUrl).TrimEnd('/');

    public IBoardSearcher CreateSearcher(IBrowserSurface browser, IActionPacer pacer) =>
        new LinkedInSearcher(browser, pacer, (logs ?? NullLoggerFactory.Instance).CreateLogger<LinkedInSearcher>(), BaseUrl);

    public const string BoardId = "linkedin";

    /// <summary>LinkedIn's geoId for the United States — used when no geoId option is set and the
    /// location is the default.</summary>
    public const string UnitedStatesGeoId = "103644278";

    public string Id => BoardId;
    public string DisplayName => "LinkedIn";
    public string SignInUrl => $"{BaseUrl}/login";
    public string HomeUrl => $"{BaseUrl}/jobs/";

    /// <summary>LinkedIn's authentication cookie — present exactly while an account is signed in.</summary>
    public string? SessionCookieName => "li_at";
    public string QuickApplyName => "Easy Apply";

    /// <summary>The messaging overlay (inside the #interop-outlet shadow root on the 2026 site) and
    /// the "Get job alerts for this search" aside. Both read live 2026-09-30.</summary>
    public IReadOnlyList<string> HiddenClutter =>
    [
        "#msg-overlay",
        ".msg-overlay-list-bubble",
        "div:has(> div > [data-view-name=\"job-search-job-alert-toggle\"])",
    ];

    /// <summary>
    /// LinkedIn's salary filter only offers fixed "$N+" buckets. The minimum is rounded DOWN to a
    /// bucket so no qualifying job is lost server-side; <see cref="Scoring.FitScorer"/> then applies
    /// the exact minimum to what each posting actually states.
    /// </summary>
    internal static readonly int[] SalaryBuckets = [40_000, 60_000, 80_000, 100_000, 120_000, 140_000, 160_000, 180_000, 200_000];

    public string BuildSearchUrl(string searchTerm, HuntFilters filters, int page = 0)
    {
        var q = new List<(string Key, string Value)>
        {
            ("keywords", searchTerm.Trim()),
        };

        if (!string.IsNullOrWhiteSpace(filters.Location)) q.Add(("location", filters.Location.Trim()));
        var geoId = filters.BoardOptions.GetValueOrDefault($"{BoardId}.geoId");
        if (string.IsNullOrWhiteSpace(geoId) && string.Equals(filters.Location?.Trim(), "United States", StringComparison.OrdinalIgnoreCase))
            geoId = UnitedStatesGeoId;
        if (!string.IsNullOrWhiteSpace(geoId)) q.Add(("geoId", geoId));
        if (filters.DistanceMiles is > 0) q.Add(("distance", filters.DistanceMiles.Value.ToString()));

        if (filters.QuickApplyOnly) q.Add(("f_AL", "true"));

        var workplaces = filters.Workplaces.Select(WorkplaceCode).Where(c => c != null).Distinct().ToList();
        if (workplaces.Count > 0) q.Add(("f_WT", string.Join(',', workplaces)));

        var jobTypes = filters.EmploymentTypes.Select(JobTypeCode).Where(c => c != null).Distinct().ToList();
        if (jobTypes.Count > 0) q.Add(("f_JT", string.Join(',', jobTypes)));

        var levels = filters.ExperienceLevels.Distinct().Select(l => ((int)l + 1).ToString()).ToList();
        if (levels.Count > 0) q.Add(("f_E", string.Join(',', levels)));

        var posted = filters.DatePosted switch
        {
            DatePosted.PastDay => "r86400",
            DatePosted.PastWeek => "r604800",
            DatePosted.PastMonth => "r2592000",
            _ => null,
        };
        if (posted != null) q.Add(("f_TPR", posted));

        if (SalaryBucketFor(filters.MinAnnualSalary) is { } bucket) q.Add(("f_SB2", bucket.ToString()));
        if (filters.UnderTenApplicants) q.Add(("f_EA", "true"));

        q.Add(("sortBy", filters.Sort == SortOrder.MostRelevant ? "R" : "DD"));
        if (page > 0) q.Add(("start", (page * 25).ToString()));

        return $"{BaseUrl}/jobs/search/?" +
               string.Join('&', q.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    /// <summary>The 1-based f_SB2 bucket at or below <paramref name="minAnnual"/>; null below the
    /// lowest bucket or when no minimum is set.</summary>
    internal static int? SalaryBucketFor(int? minAnnual)
    {
        if (minAnnual is not > 0) return null;
        var index = Array.FindLastIndex(SalaryBuckets, b => b <= minAnnual.Value);
        return index < 0 ? null : index + 1;
    }

    private static string? WorkplaceCode(WorkplaceType w) => w switch
    {
        WorkplaceType.OnSite => "1",
        WorkplaceType.Remote => "2",
        WorkplaceType.Hybrid => "3",
        _ => null,
    };

    /// <summary>LinkedIn has no contract-to-hire type; those are listed as Contract.</summary>
    private static string? JobTypeCode(EmploymentType t) => t switch
    {
        EmploymentType.FullTime => "F",
        EmploymentType.PartTime => "P",
        EmploymentType.Contract or EmploymentType.ContractToHire => "C",
        EmploymentType.Temporary => "T",
        EmploymentType.Internship => "I",
        _ => null,
    };
}
