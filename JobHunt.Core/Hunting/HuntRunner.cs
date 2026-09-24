using AutoWebNav;
using JobHunt.Core.Boards;
using JobHunt.Core.Data;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Hunting;

public enum HuntStop
{
    Finished,
    Cancelled,
    /// <summary>The board showed a CAPTCHA / verification — the account holder must answer it.</summary>
    Challenge,
    SignedOut,
    /// <summary>A results page never showed a list the searcher recognized (markup changed?).</summary>
    Unrecognized,
    /// <summary>Something unexpected went wrong; the log has the exception.</summary>
    Failed,
}

/// <summary>Progress for the UI: what the hunt is doing right now and the running totals.</summary>
public sealed record HuntProgress(string Message, int Seen, int New, int Recommended);

public sealed record HuntSummary(HuntStop Stop, int Seen, int New, int Recommended, string Message);

/// <summary>
/// Runs a user's job requirements against a board: every search term, page by page, every card —
/// de-duplicated across terms and pages — each stored, given a provisional listing-level score,
/// and marked Recommended, Found (below the threshold) or FilteredOut. Jobs the user already acted
/// on (applied, dismissed, mid-application) are never moved.
/// <para>
/// The score is provisional because it only knows what the listing states. The job-understanding
/// step (the LLM reading the description) re-scores with requirements matched to the profile.
/// </para>
/// </summary>
public sealed class HuntRunner(
    IDbContextFactory<JobHuntDb> dbFactory,
    JobStore jobs,
    FitScorer scorer,
    TimeProvider clock,
    ILogger<HuntRunner> log)
{
    /// <summary>Details already read within this long are not re-opened on a later hunt.</summary>
    public TimeSpan DetailsFreshFor { get; init; } = TimeSpan.FromDays(3);

    public async Task<HuntSummary> RunAsync(
        UserProfile user, SearchProfile search, ISearchableBoard board, IBrowserSurface browser,
        IActionPacer pacer, IProgress<HuntProgress>? progress, CancellationToken ct)
    {
        var searcher = board.CreateSearcher(browser, pacer);
        var run = await StartRunAsync(search, ct);
        var seenIds = new HashSet<string>();
        int seen = 0, fresh = 0, recommended = 0;
        var stop = HuntStop.Finished;
        var message = "";

        void Report(string m) => progress?.Report(new HuntProgress(m, seen, fresh, recommended));
        log.LogInformation("Hunt started for user {User}: {Terms} on {Board}", user.Id, string.Join(", ", search.SearchTerms), board.DisplayName);

        try
        {
            foreach (var term in search.SearchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                for (var page = 0; page < Math.Max(1, search.Filters.MaxPagesPerTerm); page++)
                {
                    ct.ThrowIfCancellationRequested();
                    Report($"Searching \"{term}\", page {page + 1}…");
                    var results = await searcher.OpenResultsAsync(board.BuildSearchUrl(term, search.Filters, page), ct);

                    if (results.State is ResultsState.Challenge or ResultsState.SignedOut or ResultsState.Unrecognized)
                    {
                        (stop, message) = StopFor(results.State, board);
                        return await FinishAsync();
                    }
                    if (results.State == ResultsState.NoResults || results.Cards.Count == 0) break;

                    foreach (var card in results.Cards)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!seenIds.Add(card.ExternalId))
                        {
                            await jobs.UpsertFoundAsync(user.Id, searcher.ToPosting(card, null, clock.GetUtcNow()), term, ct);
                            continue;
                        }
                        seen++;
                        Report($"Reading \"{card.Title}\" at {card.Company}…");
                        var outcome = await CollectAsync(user, search, searcher, card, term, pacer, ct);
                        if (outcome.IsNew) fresh++;
                        if (outcome.Status == JobStatus.Recommended) recommended++;
                    }

                    if (results.Cards.Count < searcher.PageSize) break;   // last page
                    await pacer.PauseAsync(ct);
                }
            }
            message = $"Hunt finished: {seen} jobs seen, {fresh} new, {recommended} best fits.";
        }
        catch (OperationCanceledException)
        {
            stop = HuntStop.Cancelled;
            message = $"Hunt stopped: {seen} jobs seen, {fresh} new, {recommended} best fits so far.";
        }
        catch (BoardChallengeException)
        {
            (stop, message) = StopFor(ResultsState.Challenge, board);
        }
        catch (BoardSignedOutException)
        {
            (stop, message) = StopFor(ResultsState.SignedOut, board);
        }
        catch (Exception ex)
        {
            // Never leave a run unrecorded, whatever went wrong.
            log.LogError(ex, "Hunt failed for user {User}", user.Id);
            stop = HuntStop.Failed;
            message = $"The hunt stopped after {seen} jobs: {ex.Message}";
        }
        return await FinishAsync();

        async Task<HuntSummary> FinishAsync()
        {
            await FinishRunAsync(run, seen, fresh, recommended, stop == HuntStop.Finished ? "" : message);
            log.LogInformation("Hunt for user {User} ended ({Stop}): {Seen} seen, {New} new, {Recommended} recommended",
                user.Id, stop, seen, fresh, recommended);
            Report(message);
            return new HuntSummary(stop, seen, fresh, recommended, message);
        }
    }

    private static (HuntStop, string) StopFor(ResultsState state, IJobBoard board) => state switch
    {
        ResultsState.Challenge => (HuntStop.Challenge,
            $"{board.DisplayName} wants to verify it's you. Finish that in the {board.DisplayName} pane, then run the hunt again."),
        ResultsState.SignedOut => (HuntStop.SignedOut,
            $"Signed out of {board.DisplayName}. Sign in in the {board.DisplayName} pane (or save the password on the Applicant tab), then run the hunt again."),
        _ => (HuntStop.Unrecognized,
            $"{board.DisplayName}'s results page didn't look like a job list. The hunt stopped rather than guess; the log has the URL."),
    };

    private sealed record Collected(bool IsNew, JobStatus Status);

    private async Task<Collected> CollectAsync(UserProfile user, SearchProfile search, IBoardSearcher searcher,
        ListingCard card, string term, IActionPacer pacer, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var listed = searcher.ToPosting(card, null, now);
        var (job, isNew) = await jobs.UpsertFoundAsync(user.Id, listed, term, ct);

        var needsDetails = job.DetailsReadAt is not { } read || now - read > DetailsFreshFor || job.RawDescription.Length == 0;
        if (needsDetails && !IsSettled(job.Status))
        {
            ListingDetails? details = null;
            try { details = await searcher.OpenListingAsync(card, ct); }
            catch (TimeoutException ex)
            {
                // One page that won't answer shouldn't end the hunt; keep what the card said.
                log.LogWarning(ex, "Details for job {Id} timed out; keeping the listing only", card.ExternalId);
            }
            if (details != null)
            {
                ApplyListing(job, searcher.ToPosting(card, details, now));
                job.DetailsReadAt = now;
            }
            await pacer.PauseAsync(ct);
        }

        if (!IsSettled(job.Status))
        {
            var fit = scorer.Evaluate(job, search, user, now);
            job.Score = fit.Score;
            job.ScoreBreakdown = [.. fit.Breakdown];
            job.FilterReasons = [.. fit.Rejections];
            job.Status = !fit.Passed ? JobStatus.FilteredOut
                : fit.Score >= search.ScoreThreshold ? JobStatus.Recommended
                : JobStatus.Found;
        }
        await jobs.SaveHuntResultAsync(job, ct);
        return new Collected(isNew, job.Status);
    }

    /// <summary>The user (or an application run) already decided this job's fate.</summary>
    internal static bool IsSettled(JobStatus s) =>
        s is JobStatus.Applied or JobStatus.Applying or JobStatus.NeedsYou or JobStatus.Dismissed or JobStatus.Failed;

    /// <summary>Copies what the listing states onto the stored job — but never over what the
    /// job-understanding step already worked out from the description.</summary>
    internal static void ApplyListing(JobPosting job, JobPosting listed)
    {
        job.Title = Prefer(listed.Title, job.Title);
        job.Company = Prefer(listed.Company, job.Company);
        job.Location = Prefer(listed.Location, job.Location);
        job.RawDescription = Prefer(listed.RawDescription, job.RawDescription);
        job.SupportsQuickApply = listed.SupportsQuickApply;
        job.BoardShowsApplied |= listed.BoardShowsApplied;
        job.PostedAt = listed.PostedAt ?? job.PostedAt;
        job.ApplicantCount = listed.ApplicantCount ?? job.ApplicantCount;
        if (job.UnderstoodAt is not null) return;
        if (listed.Workplace != WorkplaceType.Unknown) job.Workplace = listed.Workplace;
        if (listed.EmploymentType != EmploymentType.Unknown) job.EmploymentType = listed.EmploymentType;
        if (listed.SalaryMax is not null)
        {
            (job.SalaryMin, job.SalaryMax, job.SalaryPeriod, job.SalaryText, job.SalaryCurrency) =
                (listed.SalaryMin, listed.SalaryMax, listed.SalaryPeriod, listed.SalaryText, listed.SalaryCurrency);
        }
    }

    private static string Prefer(string? incoming, string current) => string.IsNullOrWhiteSpace(incoming) ? current : incoming;

    private async Task<HuntRun> StartRunAsync(SearchProfile search, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var run = new HuntRun { SearchProfileId = search.Id, StartedAt = clock.GetUtcNow() };
        db.HuntRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task FinishRunAsync(HuntRun run, int seen, int fresh, int recommended, string error)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        run.FinishedAt = clock.GetUtcNow();
        (run.JobsSeen, run.JobsNew, run.JobsRecommended, run.Error) = (seen, fresh, recommended, error);
        db.HuntRuns.Update(run);
        await db.Searches.Where(s => s.Id == run.SearchProfileId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastRunAt, run.FinishedAt));
        await db.SaveChangesAsync();
    }
}
