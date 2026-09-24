using JobHunt.Core.Applications;

namespace JobHunt.Core.Jobs;

public enum WorkplaceType { Unknown, OnSite, Remote, Hybrid }

public enum EmploymentType { Unknown, FullTime, PartTime, Contract, ContractToHire, Temporary, Internship }

public enum ContractTerms { Unknown, W2, Ten99, CorpToCorp }

public enum PayPeriod { Year, Month, Week, Day, Hour }

public enum JobStatus
{
    /// <summary>Collected by a search, not yet understood or scored.</summary>
    Found,
    /// <summary>Failed a hard filter — <see cref="JobPosting.FilterReasons"/> says which.</summary>
    FilteredOut,
    /// <summary>Passed the filters and scored; shown in the recommendation list.</summary>
    Recommended,
    /// <summary>The user hid it.</summary>
    Dismissed,
    Applying,
    /// <summary>Parked mid-application on a question only the user can answer.</summary>
    NeedsYou,
    Applied,
    /// <summary>An application attempt failed; the reason is on the latest application event.</summary>
    Failed,
}

/// <summary>
/// One job on one job board, for one user, keyed by (<see cref="UserProfileId"/>, <see cref="BoardId"/>,
/// <see cref="ExternalId"/>) so the same posting found by several of that user's searches is stored
/// once — and two users can each have their own score, documents and applications for it. Holds
/// what the board listed, what the LLM understood from the description (plain-English description, employment type, duration, salary,
/// requirements), the fit score, and the tailored documents generated for it.
/// </summary>
public sealed class JobPosting
{
    public int Id { get; set; }
    /// <summary>The user this job was found for.</summary>
    public int UserProfileId { get; set; }

    // ── Identity ────────────────────────────────────────────────────────────────────────────────
    /// <summary>Which <see cref="Boards.IJobBoard"/> this came from ("linkedin").</summary>
    public string BoardId { get; set; } = "";
    /// <summary>The board's own job id.</summary>
    public string ExternalId { get; set; } = "";
    public string Url { get; set; } = "";

    // ── As listed ───────────────────────────────────────────────────────────────────────────────
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string CompanyUrl { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTimeOffset? PostedAt { get; set; }
    public int? ApplicantCount { get; set; }
    /// <summary>The board's one-click application (LinkedIn Easy Apply) is available.</summary>
    public bool SupportsQuickApply { get; set; }
    /// <summary>The board already shows this job as applied to.</summary>
    public bool BoardShowsApplied { get; set; }
    public string RawDescription { get; set; } = "";
    /// <summary>When the details pane was last read. Hunts skip re-reading fresh ones.</summary>
    public DateTimeOffset? DetailsReadAt { get; set; }

    // ── Understood (LLM-extracted, code-checked) ────────────────────────────────────────────────
    public WorkplaceType Workplace { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public ContractTerms ContractTerms { get; set; }
    public int? ContractMonths { get; set; }
    /// <summary>Duration as the posting phrased it — "6 months, likely to extend".</summary>
    public string ContractDurationText { get; set; } = "";
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public PayPeriod? SalaryPeriod { get; set; }
    public string SalaryCurrency { get; set; } = "USD";
    public string SalaryText { get; set; } = "";
    public bool? SponsorshipAvailable { get; set; }
    public bool? ClearanceRequired { get; set; }
    public bool? IsStaffingAgency { get; set; }
    public int? RequiredYears { get; set; }
    /// <summary>The LLM's rewrite of the posting in plain English — what the job actually is.</summary>
    public string PlainDescription { get; set; } = "";
    public List<string> Highlights { get; set; } = [];
    public List<string> RedFlags { get; set; } = [];
    public List<string> TechStack { get; set; } = [];
    public List<JobRequirement> Requirements { get; set; } = [];
    /// <summary>Field name → the sentence of the posting it was extracted from, so every
    /// extracted fact can be shown with its evidence.</summary>
    public Dictionary<string, string> Evidence { get; set; } = [];
    public DateTimeOffset? UnderstoodAt { get; set; }

    // ── Scoring ─────────────────────────────────────────────────────────────────────────────────
    public int? Score { get; set; }
    public List<ScoreComponent> ScoreBreakdown { get; set; } = [];
    public List<string> FilterReasons { get; set; } = [];

    // ── Tailored documents ──────────────────────────────────────────────────────────────────────
    public string ResumePath { get; set; } = "";
    /// <summary>SHA-256 of the résumé as generated. A file on disk that no longer matches was
    /// edited by the user, and regeneration must not overwrite it without asking.</summary>
    public string ResumeGeneratedHash { get; set; } = "";
    public string CoverLetterPath { get; set; } = "";
    public string CoverLetterGeneratedHash { get; set; } = "";
    public DateTimeOffset? DocumentsGeneratedAt { get; set; }

    // ── Tracking ────────────────────────────────────────────────────────────────────────────────
    public JobStatus Status { get; set; }
    /// <summary>Checked for the next Apply (N) run.</summary>
    public bool SelectedForApply { get; set; }
    /// <summary>Names of the searches that found it.</summary>
    public List<string> FoundBy { get; set; } = [];
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<JobApplication> Applications { get; set; } = [];

    /// <summary>Salary as a yearly figure (hourly × 2080, daily × 260, weekly × 52, monthly × 12), for
    /// comparison against a yearly minimum. Null when the posting states no salary.</summary>
    public (decimal? Min, decimal? Max) AnnualizedSalary()
    {
        var factor = SalaryPeriod switch
        {
            PayPeriod.Hour => 2080m, PayPeriod.Day => 260m, PayPeriod.Week => 52m, PayPeriod.Month => 12m, _ => 1m,
        };
        return (SalaryMin * factor, SalaryMax * factor);
    }
}

public enum RequirementKind { Required, NiceToHave }

/// <summary>One requirement from a posting, with the profile facts (<c>FactRef</c>s) that satisfy it.</summary>
public sealed class JobRequirement
{
    public string Text { get; set; } = "";
    public RequirementKind Kind { get; set; }
    public List<string> MatchedFactRefs { get; set; } = [];

    public bool IsMet => MatchedFactRefs.Count > 0;
}

/// <summary>One criterion's contribution to a fit score.</summary>
public sealed class ScoreComponent
{
    public string Criterion { get; set; } = "";
    public double Weight { get; set; }
    public double Earned { get; set; }
    /// <summary>Human-readable reason — "Matched 9/11 required".</summary>
    public string Detail { get; set; } = "";
}
