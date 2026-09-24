using JobHunt.Core.Applications;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class JobStoreTests
{
    private TestDb db = null!;
    private TestClock clock = null!;
    private JobStore store = null!;
    private int user;
    private int otherUser;

    [SetUp]
    public async Task SetUp()
    {
        db = new TestDb();
        clock = new TestClock(new DateTimeOffset(2026, 9, 23, 15, 0, 0, TimeSpan.Zero));
        store = new JobStore(db, clock, NullLogger<JobStore>.Instance);
        var profiles = new ProfileStore(db, clock, NullLogger<ProfileStore>.Instance);
        user = (await profiles.SaveAsync(ProfileStoreTests.Sample())).Id;
        otherUser = (await profiles.SaveAsync(ProfileStoreTests.Sample("Al", "Baker"))).Id;
    }

    [TearDown]
    public void TearDown() => db.Dispose();

    private static JobPosting Found(string id, string title = "Senior .NET Developer") => new()
    {
        BoardId = "linkedin", ExternalId = id, Title = title, Company = "Acme",
        Url = $"https://www.linkedin.com/jobs/view/{id}/", SupportsQuickApply = true,
    };

    [Test]
    public async Task Upsert_SamePostingFromTwoSearches_IsStoredOnce()
    {
        var (first, isNew1) = await store.UpsertFoundAsync(user, Found("4001"), ".NET Developer");
        var (second, isNew2) = await store.UpsertFoundAsync(user, Found("4001"), "Full Stack Engineer");

        Assert.Multiple(() =>
        {
            Assert.That(isNew1, Is.True);
            Assert.That(isNew2, Is.False);
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(second.FoundBy, Is.EquivalentTo(new[] { ".NET Developer", "Full Stack Engineer" }));
        });
    }

    [Test]
    public async Task Upsert_KeepsWhatWasDerived()
    {
        var (job, _) = await store.UpsertFoundAsync(user, Found("4002"), "s");
        job.Status = JobStatus.Recommended;
        job.Score = 88;
        job.PlainDescription = "Build billing APIs.";
        job.Workplace = WorkplaceType.Remote;
        job.SalaryMin = 140_000; job.SalaryMax = 160_000; job.SalaryPeriod = PayPeriod.Year;
        await store.SaveAsync(job);

        await store.UpsertFoundAsync(user, Found("4002", "Sr. .NET Developer"), "s");
        var recs = await store.RecommendationsAsync(user);

        Assert.Multiple(() =>
        {
            Assert.That(recs.Single().Score, Is.EqualTo(88));
            Assert.That(recs.Single().PlainDescription, Is.EqualTo("Build billing APIs."));
            Assert.That(recs.Single().SalaryMax, Is.EqualTo(160_000m));
            Assert.That(recs.Single().Title, Is.EqualTo("Sr. .NET Developer"), "listing fields refresh");
        });
    }

    [Test]
    public void Rank_BestScoreFirst_ThenNewest()
    {
        var older = new JobPosting { Score = 80, PostedAt = clock.Now.AddDays(-5) };
        var newer = new JobPosting { Score = 80, PostedAt = clock.Now.AddDays(-1) };
        var best = new JobPosting { Score = 95, PostedAt = clock.Now.AddDays(-9) };
        Assert.That(JobStore.Rank([older, newer, best]), Is.EqualTo(new[] { best, newer, older }));
    }

    [Test]
    public async Task DryRun_IsLogged_ButLeavesTheJobUnapplied()
    {
        var (job, _) = await store.UpsertFoundAsync(user, Found("4003"), "s");
        job.Status = JobStatus.Recommended;
        await store.SaveAsync(job);

        await store.RecordApplicationAsync(new JobApplication
        {
            JobPostingId = job.Id, IsDryRun = true, Method = "LinkedIn Easy Apply",
            RunLog = ["Opened posting", "Uploaded résumé", "DRY RUN: stopped before Submit"],
        });

        var log = await store.ApplicationLogAsync(user);
        var recs = await store.RecommendationsAsync(user);
        Assert.Multiple(async () =>
        {
            Assert.That(log.Single().IsDryRun, Is.True);
            Assert.That(log.Single().RunLog, Has.Count.EqualTo(3));
            Assert.That(log.Single().Events.Single().Note, Does.Contain("nothing was sent"));
            Assert.That(recs.Single().Status, Is.EqualTo(JobStatus.Recommended), "a rehearsal is not an application");
            Assert.That(await store.RealApplicationsTodayAsync(user), Is.Zero);
        });
    }

    [Test]
    public async Task RealApplication_ThenOutcomes_BuildAHistory()
    {
        var (job, _) = await store.UpsertFoundAsync(user, Found("4004"), "s");
        var app = await store.RecordApplicationAsync(new JobApplication { JobPostingId = job.Id, Method = "LinkedIn Easy Apply" });

        clock.Now = clock.Now.AddDays(3);
        await store.RecordOutcomeAsync(app.Id, ApplicationOutcome.Interviewing, OutcomeSource.Manual, "Phone screen Friday");
        clock.Now = clock.Now.AddDays(10);
        await store.RecordOutcomeAsync(app.Id, ApplicationOutcome.Rejected, OutcomeSource.Board);

        var logged = (await store.ApplicationLogAsync(user, includeDryRuns: false)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(logged.Outcome, Is.EqualTo(ApplicationOutcome.Rejected));
            Assert.That(logged.Events.OrderBy(e => e.At).Select(e => e.Outcome),
                Is.EqualTo(new[] { ApplicationOutcome.Submitted, ApplicationOutcome.Interviewing, ApplicationOutcome.Rejected }));
            Assert.That(logged.JobPosting.Status, Is.EqualTo(JobStatus.Applied));
        });
    }

    [Test]
    public async Task Selection_ReplacesThePreviousSelection()
    {
        var (a, _) = await store.UpsertFoundAsync(user, Found("5001"), "s");
        var (b, _) = await store.UpsertFoundAsync(user, Found("5002"), "s");
        await store.SetSelectedAsync(user, [a.Id]);
        await store.SetSelectedAsync(user, [b.Id]);

        await using var ctx = db.CreateDbContext();
        Assert.That(ctx.Jobs.Where(j => j.SelectedForApply).Select(j => j.Id).ToList(), Is.EqualTo(new[] { b.Id }));
    }

    [Test]
    public async Task EachUser_HasTheirOwnCopyOfAPosting_AndTheirOwnLog()
    {
        var (mine, _) = await store.UpsertFoundAsync(user, Found("6001"), "s");
        var (theirs, theirsIsNew) = await store.UpsertFoundAsync(otherUser, Found("6001"), "s");
        mine.Status = JobStatus.Recommended; mine.Score = 90;
        await store.SaveAsync(mine);
        await store.RecordApplicationAsync(new JobApplication { JobPostingId = mine.Id, Method = "LinkedIn Easy Apply" });

        Assert.Multiple(async () =>
        {
            Assert.That(theirsIsNew, Is.True, "same LinkedIn job, different user: a separate row");
            Assert.That(theirs.Id, Is.Not.EqualTo(mine.Id));
            Assert.That(await store.RecommendationsAsync(otherUser), Is.Empty);
            Assert.That(await store.ApplicationLogAsync(otherUser), Is.Empty);
            Assert.That(await store.ApplicationLogAsync(user), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Selection_CannotTouchAnotherUsersJobs()
    {
        var (theirs, _) = await store.UpsertFoundAsync(otherUser, Found("7001"), "s");
        await store.SetSelectedAsync(user, [theirs.Id]);
        await using var ctx = db.CreateDbContext();
        Assert.That(ctx.Jobs.Single(j => j.Id == theirs.Id).SelectedForApply, Is.False);
    }

    [TestCase(-6)]   // US Central
    [TestCase(10)]   // Sydney
    public async Task DailyCap_CountsTheLocalDay_InAnyTimeZone(int offsetHours)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(offsetHours), "test", "test");
        var local = new ZonedClock(zone);
        var store2 = new JobStore(db, local, NullLogger<JobStore>.Instance);
        var (job, _) = await store2.UpsertFoundAsync(user, Found("8001"), "s");

        // Yesterday evening, local time — must not count against today.
        local.Now = new DateTimeOffset(2026, 9, 22, 21, 0, 0, TimeSpan.FromHours(offsetHours)).ToUniversalTime();
        await store2.RecordApplicationAsync(new JobApplication { JobPostingId = job.Id, Method = "x" });
        // This morning, local time — must count.
        local.Now = new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.FromHours(offsetHours)).ToUniversalTime();
        await store2.RecordApplicationAsync(new JobApplication { JobPostingId = job.Id, Method = "x" });

        local.Now = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.FromHours(offsetHours)).ToUniversalTime();
        Assert.That(await store2.RealApplicationsTodayAsync(user), Is.EqualTo(1));
    }

    [Test]
    public async Task AHuntSave_NeverRevertsTheUsersSelectionOrAnApplication()
    {
        var (job, _) = await store.UpsertFoundAsync(user, Found("9001"), "s");
        var huntsCopy = job;                          // what a hunt holds while it works
        await store.SetSelectedAsync(user, [job.Id]); // meanwhile the user ticks it
        await store.RecordApplicationAsync(new JobApplication { JobPostingId = job.Id, Method = "x" }); // and it's applied to

        huntsCopy.Status = JobStatus.Recommended;
        huntsCopy.Score = 77;
        await store.SaveHuntResultAsync(huntsCopy);

        await using var ctx = db.CreateDbContext();
        var row = ctx.Jobs.Single(j => j.Id == job.Id);
        Assert.Multiple(() =>
        {
            Assert.That(row.Status, Is.EqualTo(JobStatus.Applied), "an applied job stays applied");
            Assert.That(ctx.Applications.Count(a => a.JobPostingId == job.Id), Is.EqualTo(1));
        });
    }

    private sealed class ZonedClock(TimeZoneInfo zone) : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => zone;
    }

    [Test]
    public async Task Selection_ReturnsTheCountOfSelectedBestFits()
    {
        var (a, _) = await store.UpsertFoundAsync(user, Found("5101"), "s");
        var (b, _) = await store.UpsertFoundAsync(user, Found("5102"), "s");
        a.Status = b.Status = JobStatus.Recommended;
        await store.SaveAsync(a); await store.SaveAsync(b);
        var (theirs, _) = await store.UpsertFoundAsync(otherUser, Found("5103"), "s");

        Assert.That(await store.SetSelectedAsync(user, [a.Id, b.Id, theirs.Id, 999_999]), Is.EqualTo(2),
            "another applicant's job and unknown ids don't count");
    }
}
