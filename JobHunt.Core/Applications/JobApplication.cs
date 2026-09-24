using JobHunt.Core.Jobs;

namespace JobHunt.Core.Applications;

/// <summary>Where an application stands, from the applicant's side.</summary>
public enum ApplicationOutcome
{
    Submitted,
    /// <summary>The board reports the employer opened it.</summary>
    Viewed,
    InReview,
    Interviewing,
    Offer,
    Accepted,
    Rejected,
    Withdrawn,
    /// <summary>No reply after long enough that the user (or a rule) called it.</summary>
    NoResponse,
    /// <summary>The posting closed without a decision.</summary>
    Closed,
}

public enum OutcomeSource
{
    /// <summary>The user set it.</summary>
    Manual,
    /// <summary>Read from the board's own application-status page.</summary>
    Board,
    /// <summary>Recorded by the Apply run itself.</summary>
    Automation,
}

/// <summary>
/// One submitted application — the permanent record of what was sent where, with which documents
/// and answers, and everything that happened afterwards.
/// </summary>
public sealed class JobApplication
{
    public int Id { get; set; }
    public int JobPostingId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;

    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>
    /// A rehearsal: the run did everything a real application does — opened the posting, filled
    /// every field, uploaded both documents, answered every question — and stopped before the final
    /// submit click. Nothing was sent to the employer. Dry run is the default until the user
    /// turns it off in Settings.
    /// </summary>
    public bool IsDryRun { get; set; }
    /// <summary>"LinkedIn Easy Apply", "Company site", "Manual".</summary>
    public string Method { get; set; } = "";

    /// <summary>The exact files uploaded. Kept even if the job's documents are regenerated later.</summary>
    public string ResumePath { get; set; } = "";
    public string ResumeHash { get; set; } = "";
    public string CoverLetterPath { get; set; } = "";
    public string CoverLetterHash { get; set; } = "";
    /// <summary>Every screening question and the answer given.</summary>
    public List<AnsweredQuestion> Answers { get; set; } = [];
    /// <summary>What the run did, step by step, in order — the same lines the application log
    /// file gets, kept with the application so it can be reviewed from the app.</summary>
    public List<string> RunLog { get; set; } = [];

    public ApplicationOutcome Outcome { get; set; }
    public DateTimeOffset OutcomeUpdatedAt { get; set; }
    public string Notes { get; set; } = "";
    public List<ApplicationEvent> Events { get; set; } = [];

    /// <summary>Moves the application to <paramref name="outcome"/> and logs the change.</summary>
    public ApplicationEvent RecordOutcome(ApplicationOutcome outcome, OutcomeSource source, DateTimeOffset at, string note = "")
    {
        var evt = new ApplicationEvent { At = at, Outcome = outcome, Source = source, Note = note };
        Events.Add(evt);
        Outcome = outcome;
        OutcomeUpdatedAt = at;
        return evt;
    }
}

public sealed class AnsweredQuestion
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    /// <summary>"answer bank", "profile", "LLM (confirmed by user)".</summary>
    public string Source { get; set; } = "";
}

/// <summary>One dated entry in an application's history.</summary>
public sealed class ApplicationEvent
{
    public int Id { get; set; }
    public int JobApplicationId { get; set; }
    public DateTimeOffset At { get; set; }
    public ApplicationOutcome Outcome { get; set; }
    public OutcomeSource Source { get; set; }
    public string Note { get; set; } = "";
}
