using JobHunt.Core.Applications;
using JobHunt.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Jobs;

/// <summary>Jobs and the application log: upserting what searches find, the recommendation list,
/// the Apply (N) selection, and recording applications and their outcomes.</summary>
public sealed class JobStore(IDbContextFactory<JobHuntDb> dbFactory, TimeProvider clock, ILogger<JobStore> log)
{
    /// <summary>
    /// Stores a posting a search found for <paramref name="userId"/>. A posting that user already has
    /// (same board + external id) keeps
    /// everything derived from it — score, documents, status, applications — and only gets its
    /// listing fields refreshed and the search name added. Returns the stored row and whether it
    /// was new.
    /// </summary>
    public async Task<(JobPosting Job, bool IsNew)> UpsertFoundAsync(int userId, JobPosting found, string foundBy, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var existing = await db.Jobs.FirstOrDefaultAsync(
            j => j.UserProfileId == userId && j.BoardId == found.BoardId && j.ExternalId == found.ExternalId, ct);
        if (existing is null)
        {
            found.Id = 0;
            found.UserProfileId = userId;
            found.FirstSeenAt = found.LastSeenAt = now;
            found.Status = JobStatus.Found;
            if (!found.FoundBy.Contains(foundBy)) found.FoundBy.Add(foundBy);
            db.Jobs.Add(found);
            await db.SaveChangesAsync(ct);
            log.LogInformation("New job {Board}/{ExternalId}: {Title} at {Company}", found.BoardId, found.ExternalId, found.Title, found.Company);
            return (found, true);
        }

        existing.LastSeenAt = now;
        existing.Title = Prefer(found.Title, existing.Title);
        existing.Company = Prefer(found.Company, existing.Company);
        existing.Location = Prefer(found.Location, existing.Location);
        existing.Url = Prefer(found.Url, existing.Url);
        existing.RawDescription = Prefer(found.RawDescription, existing.RawDescription);
        existing.PostedAt ??= found.PostedAt;
        existing.ApplicantCount = found.ApplicantCount ?? existing.ApplicantCount;
        // A card only ever adds Easy Apply: an undrawn card never says so, and the details pane
        // (SaveHuntResultAsync) is what decides it.
        existing.SupportsQuickApply |= found.SupportsQuickApply;
        existing.BoardShowsApplied |= found.BoardShowsApplied;
        if (!existing.FoundBy.Contains(foundBy)) existing.FoundBy = [.. existing.FoundBy, foundBy];
        await db.SaveChangesAsync(ct);
        return (existing, false);
    }

    /// <summary>
    /// Writes what a hunt learned about a job onto the row as it is NOW — never a stale copy of the
    /// whole row — so the user's Apply selection, a status an application set meanwhile, and the
    /// application history are left alone. Status and score only move while the job is unsettled.
    /// </summary>
    public async Task SaveHuntResultAsync(JobPosting learned, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.Jobs.FirstOrDefaultAsync(j => j.Id == learned.Id, ct);
        if (row is null) return;
        row.Title = learned.Title;
        row.Company = learned.Company;
        row.Location = learned.Location;
        row.RawDescription = learned.RawDescription;
        row.SupportsQuickApply = learned.SupportsQuickApply;
        row.BoardShowsApplied |= learned.BoardShowsApplied;
        row.PostedAt = learned.PostedAt;
        row.ApplicantCount = learned.ApplicantCount;
        row.DetailsReadAt = learned.DetailsReadAt;
        if (row.UnderstoodAt is null)
        {
            row.Workplace = learned.Workplace;
            row.EmploymentType = learned.EmploymentType;
            (row.SalaryMin, row.SalaryMax, row.SalaryPeriod, row.SalaryText, row.SalaryCurrency) =
                (learned.SalaryMin, learned.SalaryMax, learned.SalaryPeriod, learned.SalaryText, learned.SalaryCurrency);
        }
        if (row.Status is JobStatus.Found or JobStatus.FilteredOut or JobStatus.Recommended)
        {
            row.Status = learned.Status;
            row.Score = learned.Score;
            row.ScoreBreakdown = learned.ScoreBreakdown;
            row.FilterReasons = learned.FilterReasons;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(JobPosting job, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Jobs.Update(job);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The user's recommended jobs, best fit first.</summary>
    public async Task<List<JobPosting>> RecommendationsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.UserProfileId == userId)
            .Where(j => j.Status == JobStatus.Recommended || j.Status == JobStatus.NeedsYou)
            .ToListAsync(ct);
        return Rank(jobs);
    }

    /// <summary>Highest score first; ties go to the newer posting.</summary>
    internal static List<JobPosting> Rank(IEnumerable<JobPosting> jobs) => jobs
        .OrderByDescending(j => j.Score ?? -1)
        .ThenByDescending(j => j.PostedAt ?? j.FirstSeenAt)
        .ToList();

    /// <summary>Replaces the user's Apply (N) selection. Ids of another user's jobs are ignored.
    /// Returns how many of the user's recommended jobs are now selected — the number Apply (N) shows.</summary>
    public async Task<int> SetSelectedAsync(int userId, IReadOnlyCollection<int> jobIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var mine = db.Jobs.Where(j => j.UserProfileId == userId);
        await mine.Where(j => j.SelectedForApply && !jobIds.Contains(j.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.SelectedForApply, false), ct);
        await mine.Where(j => jobIds.Contains(j.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.SelectedForApply, true), ct);
        return await mine.CountAsync(j => j.SelectedForApply && (j.Status == JobStatus.Recommended || j.Status == JobStatus.NeedsYou), ct);
    }

    /// <summary>Logs a finished application attempt (real or dry run) against its job.</summary>
    public async Task<JobApplication> RecordApplicationAsync(JobApplication application, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.Jobs.FirstAsync(j => j.Id == application.JobPostingId, ct);
        var now = clock.GetUtcNow();
        application.AppliedAt = now;
        application.RecordOutcome(ApplicationOutcome.Submitted, OutcomeSource.Automation, now,
            application.IsDryRun ? "Dry run — stopped before the final submit; nothing was sent." : "Submitted.");
        if (!application.IsDryRun)
        {
            job.Status = JobStatus.Applied;
            job.SelectedForApply = false;
        }
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        log.LogInformation("{Kind} application recorded for {Title} at {Company}",
            application.IsDryRun ? "Dry-run" : "Real", job.Title, job.Company);
        return application;
    }

    /// <summary>Records what happened after applying — viewed, interviewing, rejected, offer…</summary>
    public async Task RecordOutcomeAsync(int applicationId, ApplicationOutcome outcome, OutcomeSource source, string note = "", CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var app = await db.Applications.Include(a => a.Events).FirstAsync(a => a.Id == applicationId, ct);
        app.RecordOutcome(outcome, source, clock.GetUtcNow(), note);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Application {Id} is now {Outcome} ({Source})", applicationId, outcome, source);
    }

    /// <summary>The user's application log, newest first, dry runs included unless excluded.</summary>
    public async Task<List<JobApplication>> ApplicationLogAsync(int userId, bool includeDryRuns = true, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Applications.AsNoTracking()
            .Include(a => a.JobPosting).Include(a => a.Events)
            .Where(a => a.JobPosting.UserProfileId == userId)
            .Where(a => includeDryRuns || !a.IsDryRun)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(ct);
    }

    /// <summary>The user's real submissions so far today (local time) — enforced against the daily cap.</summary>
    public async Task<int> RealApplicationsTodayAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var local = clock.GetLocalNow();
        var startOfDay = new DateTimeOffset(local.Date, local.Offset);
        return await db.Applications.CountAsync(
            a => a.JobPosting.UserProfileId == userId && !a.IsDryRun && a.AppliedAt >= startOfDay, ct);
    }

    private static string Prefer(string? incoming, string current) =>
        string.IsNullOrWhiteSpace(incoming) ? current : incoming;
}
