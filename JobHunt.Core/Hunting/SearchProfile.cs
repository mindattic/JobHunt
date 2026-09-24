using JobHunt.Core.Jobs;

namespace JobHunt.Core.Hunting;

public enum DatePosted { Any, PastDay, PastWeek, PastMonth }

public enum ExperienceLevel { Internship, Entry, Associate, MidSenior, Director, Executive }

public enum SortOrder { MostRecent, MostRelevant }

/// <summary>
/// A saved hunt: several search terms (".NET Developer", "Full Stack Engineer") run on one board
/// with one shared set of filters and score weights.
/// </summary>
public sealed class SearchProfile
{
    public int Id { get; set; }
    /// <summary>The user this search belongs to.</summary>
    public int UserProfileId { get; set; }
    public string Name { get; set; } = "";
    public string BoardId { get; set; } = "linkedin";
    public bool Enabled { get; set; } = true;
    public List<string> SearchTerms { get; set; } = [];
    public HuntFilters Filters { get; set; } = new();
    public ScoreWeights Weights { get; set; } = new();
    /// <summary>Jobs scoring below this are kept but not recommended.</summary>
    public int ScoreThreshold { get; set; } = 60;
    /// <summary>Cap on how many jobs per hunt get tailored documents — bounds LLM cost.</summary>
    public int MaxDocumentsPerHunt { get; set; } = 25;
    public DateTimeOffset? LastRunAt { get; set; }
}

/// <summary>
/// Board-agnostic filters. Each <see cref="Boards.IJobBoard"/> pushes what it can into its own
/// search (URL parameters); <see cref="Scoring.FitScorer"/> enforces all of them afterwards
/// against what was actually extracted, so a board that can't filter on something still ends up
/// filtered.
/// </summary>
public sealed class HuntFilters
{
    public string Location { get; set; } = "United States";
    public int? DistanceMiles { get; set; }
    /// <summary>In order of preference — the first is scored highest. Default: remote only.</summary>
    public List<WorkplaceType> Workplaces { get; set; } = [WorkplaceType.Remote];
    public List<EmploymentType> EmploymentTypes { get; set; } =
        [EmploymentType.FullTime, EmploymentType.Contract, EmploymentType.ContractToHire];
    /// <summary>Scored higher than the other allowed types.</summary>
    public List<EmploymentType> PreferredEmploymentTypes { get; set; } = [EmploymentType.FullTime];
    public int? MinAnnualSalary { get; set; }
    public decimal? MinHourlyRate { get; set; }
    /// <summary>False keeps jobs that state no salary (flagged, half salary credit).</summary>
    public bool ExcludeUnknownSalary { get; set; }
    public int? MinContractMonths { get; set; }
    public DatePosted DatePosted { get; set; } = DatePosted.PastWeek;
    public List<ExperienceLevel> ExperienceLevels { get; set; } = [];
    public bool QuickApplyOnly { get; set; } = true;
    public bool UnderTenApplicants { get; set; }
    public int? MaxApplicants { get; set; }
    public List<string> CompanyBlocklist { get; set; } = [];
    /// <summary>Keep only jobs whose stack includes at least one of these (empty = no rule).</summary>
    public List<string> MustHaveAnyTech { get; set; } = [];
    public List<string> MustAvoidTech { get; set; } = [];
    public bool ExcludeStaffingAgencies { get; set; }
    public SortOrder Sort { get; set; } = SortOrder.MostRecent;
    public int MaxPagesPerTerm { get; set; } = 3;
    /// <summary>Board-specific extras, keyed "board.option" — e.g. "linkedin.geoId".</summary>
    public Dictionary<string, string> BoardOptions { get; set; } = [];
}

/// <summary>How much each criterion counts toward the 0–100 fit score. Normalized, so the
/// numbers are relative.</summary>
public sealed class ScoreWeights
{
    public double RequiredSkills { get; set; } = 35;
    public double NiceToHaveSkills { get; set; } = 10;
    public double TitleAlignment { get; set; } = 15;
    public double Salary { get; set; } = 15;
    public double Workplace { get; set; } = 10;
    public double EmploymentType { get; set; } = 10;
    public double Freshness { get; set; } = 5;
}

/// <summary>One execution of a <see cref="SearchProfile"/>.</summary>
public sealed class HuntRun
{
    public int Id { get; set; }
    public int SearchProfileId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int JobsSeen { get; set; }
    public int JobsNew { get; set; }
    public int JobsRecommended { get; set; }
    public string Error { get; set; } = "";
}
