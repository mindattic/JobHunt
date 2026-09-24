using AutoWebNav;
using JobHunt.Core.Boards;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class HuntRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private TestDb db = null!;
    private TestClock clock = null!;
    private JobStore jobs = null!;
    private HuntRunner runner = null!;
    private UserProfile user = null!;

    [SetUp]
    public async Task SetUp()
    {
        db = new TestDb();
        clock = new TestClock(Now);
        jobs = new JobStore(db, clock, NullLogger<JobStore>.Instance);
        runner = new HuntRunner(db, jobs, new FitScorer(), clock, NullLogger<HuntRunner>.Instance);
        user = await new ProfileStore(db, clock, NullLogger<ProfileStore>.Instance).SaveAsync(ProfileStoreTests.Sample());
    }

    [TearDown]
    public void TearDown() => db.Dispose();

    private SearchProfile Search(params string[] terms)
    {
        var s = new SearchProfile { UserProfileId = user.Id, SearchTerms = [.. terms], ScoreThreshold = 50 };
        s.Filters.MaxPagesPerTerm = 3;
        using var ctx = db.CreateDbContext();
        ctx.Searches.Add(s);
        ctx.SaveChanges();
        return s;
    }

    private Task<HuntSummary> Run(FakeBoard board, SearchProfile search, CancellationToken ct = default) =>
        runner.RunAsync(user, search, board, new NullBrowser(), new NoPacer(), null, ct);

    [Test]
    public async Task EveryTermAndPage_IsCollected_AndDuplicatesAcrossTermsAreStoredOnce()
    {
        var board = new FakeBoard(pageSize: 2);
        board.Pages[(".NET Developer", 0)] = [Card("1"), Card("2")];
        board.Pages[(".NET Developer", 1)] = [Card("3")];                     // short page → last
        board.Pages[("C# Engineer", 0)] = [Card("2"), Card("4")];              // job 2 again

        var summary = await Run(board, Search(".NET Developer", "C# Engineer"));

        await using var ctx = db.CreateDbContext();
        var stored = await ctx.Jobs.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(summary.Stop, Is.EqualTo(HuntStop.Finished));
            Assert.That(summary.Seen, Is.EqualTo(4));
            Assert.That(stored.Select(j => j.ExternalId), Is.EquivalentTo(new[] { "1", "2", "3", "4" }));
            Assert.That(stored.Single(j => j.ExternalId == "2").FoundBy, Is.EquivalentTo(new[] { ".NET Developer", "C# Engineer" }));
            Assert.That(board.PagesOpened, Is.EqualTo(4), "term 1: pages 1-2 (2 was short, so no page 3); term 2: page 1 full, page 2 empty");
            Assert.That(board.DetailsOpened, Is.EquivalentTo(new[] { "1", "2", "3", "4" }), "each job's details read once");
            Assert.That(stored.All(j => j.DetailsReadAt == Now), Is.True);
        });
    }

    [Test]
    public async Task JobsAreScored_AndSortedIntoRecommendedOrFilteredOut()
    {
        var board = new FakeBoard();
        board.Pages[(".NET Developer", 0)] = [Card("10"), Card("11")];
        board.Details["10"] = Details("10", ["Remote", "Full-time", "$150K/yr - $170K/yr"]);
        board.Details["11"] = Details("11", ["On-site", "Full-time"]);    // remote-only by default

        var summary = await Run(board, Search(".NET Developer"));

        await using var ctx = db.CreateDbContext();
        var remote = await ctx.Jobs.SingleAsync(j => j.ExternalId == "10");
        var onsite = await ctx.Jobs.SingleAsync(j => j.ExternalId == "11");
        Assert.Multiple(() =>
        {
            Assert.That(remote.Status, Is.EqualTo(JobStatus.Recommended));
            Assert.That(remote.Score, Is.GreaterThan(50));
            Assert.That(remote.ScoreBreakdown, Is.Not.Empty);
            Assert.That(remote.SalaryMax, Is.EqualTo(170_000m));
            Assert.That(onsite.Status, Is.EqualTo(JobStatus.FilteredOut));
            Assert.That(onsite.FilterReasons.Single(), Does.Contain("OnSite"));
            Assert.That(summary.Recommended, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AChallenge_StopsTheHuntImmediately()
    {
        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("1")];
        board.ChallengeOn = ("b", 0);
        board.Pages[("c", 0)] = [Card("3")];

        var summary = await Run(board, Search("a", "b", "c"));

        Assert.Multiple(async () =>
        {
            Assert.That(summary.Stop, Is.EqualTo(HuntStop.Challenge));
            Assert.That(summary.Message, Does.Contain("verify"));
            Assert.That(board.PagesOpened, Is.EqualTo(2), "nothing after the challenge");
            await using var ctx = db.CreateDbContext();
            Assert.That((await ctx.HuntRuns.SingleAsync()).Error, Does.Contain("verify"));
        });
    }

    [Test]
    public async Task AChallengeWhileReadingDetails_AlsoStops()
    {
        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("1"), Card("2")];
        board.ChallengeOnDetails = "1";
        var summary = await Run(board, Search("a"));
        Assert.That(summary.Stop, Is.EqualTo(HuntStop.Challenge));
        Assert.That(board.DetailsOpened, Is.EqualTo(new[] { "1" }));
    }

    [Test]
    public async Task JobsTheUserActedOn_AreNeverMoved()
    {
        var (applied, _) = await jobs.UpsertFoundAsync(user.Id, new JobPosting { BoardId = "linkedin", ExternalId = "7", Title = "Old" }, "x");
        applied.Status = JobStatus.Applied;
        await jobs.SaveAsync(applied);

        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("7")];
        await Run(board, Search("a"));

        await using var ctx = db.CreateDbContext();
        Assert.That((await ctx.Jobs.SingleAsync()).Status, Is.EqualTo(JobStatus.Applied));
        Assert.That(board.DetailsOpened, Is.Empty, "no need to read an applied job again");
    }

    [Test]
    public async Task FreshDetails_AreNotReadAgainOnTheNextHunt()
    {
        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("1")];
        var search = Search("a");
        await Run(board, search);
        clock.Now = Now.AddDays(1);
        await Run(board, search);
        Assert.That(board.DetailsOpened, Is.EqualTo(new[] { "1" }));

        clock.Now = Now.AddDays(5);
        await Run(board, search);
        Assert.That(board.DetailsOpened, Is.EqualTo(new[] { "1", "1" }), "stale after the freshness window");
    }

    [Test]
    public async Task Cancelling_EndsTheHuntCleanly()
    {
        using var cts = new CancellationTokenSource();
        var board = new FakeBoard { OnDetails = _ => cts.Cancel() };
        board.Pages[("a", 0)] = [Card("1"), Card("2")];
        var summary = await Run(board, Search("a"), cts.Token);
        Assert.That(summary.Stop, Is.EqualTo(HuntStop.Cancelled));
        await using var ctx = db.CreateDbContext();
        Assert.That((await ctx.HuntRuns.SingleAsync()).FinishedAt, Is.Not.Null);
    }

    [Test]
    public async Task AnUndrawnCardSeenAgain_DoesNotLoseEasyApply()
    {
        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("20")];
        board.Pages[("b", 0)] = [new ListingCard("20", "", "", "", QuickApply: false, Applied: false, Promoted: false)];
        var search = Search("a", "b");
        await Run(board, search);
        await Run(board, search);   // details still fresh — not re-read

        await using var ctx = db.CreateDbContext();
        var job = await ctx.Jobs.SingleAsync();
        Assert.That(job.SupportsQuickApply, Is.True);
        Assert.That(job.Status, Is.EqualTo(JobStatus.Recommended));
    }

    [Test]
    public async Task AnUnexpectedError_StillRecordsTheRun()
    {
        var board = new FakeBoard { OnDetails = _ => throw new InvalidOperationException("page went weird") };
        board.Pages[("a", 0)] = [Card("30")];
        var summary = await Run(board, Search("a"));

        await using var ctx = db.CreateDbContext();
        var run = await ctx.HuntRuns.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(summary.Stop, Is.EqualTo(HuntStop.Failed));
            Assert.That(run.FinishedAt, Is.Not.Null);
            Assert.That(run.Error, Does.Contain("page went weird"));
        });
    }

    [Test]
    public async Task ATimedOutJob_IsSkipped_AndTheHuntGoesOn()
    {
        var board = new FakeBoard { OnDetails = id => { if (id == "40") throw new TimeoutException("slow"); } };
        board.Pages[("a", 0)] = [Card("40"), Card("41")];
        var summary = await Run(board, Search("a"));
        Assert.That(summary.Stop, Is.EqualTo(HuntStop.Finished));
        Assert.That(summary.Seen, Is.EqualTo(2));
    }

    [Test]
    public async Task AFailedApplication_IsNotResurrected()
    {
        var (job, _) = await jobs.UpsertFoundAsync(user.Id, new JobPosting { BoardId = "linkedin", ExternalId = "50", Title = "T" }, "x");
        job.Status = JobStatus.Failed;
        await jobs.SaveAsync(job);
        var board = new FakeBoard();
        board.Pages[("a", 0)] = [Card("50")];
        await Run(board, Search("a"));
        await using var ctx = db.CreateDbContext();
        Assert.That((await ctx.Jobs.SingleAsync()).Status, Is.EqualTo(JobStatus.Failed));
    }

    private static ListingCard Card(string id) => new(id, $"Senior .NET Developer {id}", "Acme", "United States", true, false, false);

    private static ListingDetails Details(string id, string[] insights) =>
        new(id, "Senior .NET Developer", "Acme", "United States", ["United States", "1 day ago", "20 applicants"], insights, "Build APIs in C#.", "easy");

    /// <summary>A board whose pages and details are scripted, recording what the runner asked for.</summary>
    private sealed class FakeBoard(int pageSize = 25) : ISearchableBoard, IBoardSearcher
    {
        private readonly LinkedInJobBoard urls = new();
        public Dictionary<(string Term, int Page), ListingCard[]> Pages { get; } = [];
        public Dictionary<string, ListingDetails> Details { get; } = [];
        public (string Term, int Page)? ChallengeOn { get; set; }
        public string? ChallengeOnDetails { get; set; }
        public Action<string>? OnDetails { get; init; }
        public int PagesOpened { get; private set; }
        public List<string> DetailsOpened { get; } = [];

        public string Id => "linkedin";
        public string DisplayName => "LinkedIn";
        public string SignInUrl => "";
        public string HomeUrl => "";
        public string? SessionCookieName => null;
        public string QuickApplyName => "Easy Apply";
        public int PageSize => pageSize;
        public string BuildSearchUrl(string searchTerm, HuntFilters filters, int page = 0) => $"fake://{searchTerm}/{page}";
        public IBoardSearcher CreateSearcher(IBrowserSurface browser, IActionPacer pacer) => this;

        public Task<ResultsPage> OpenResultsAsync(string url, CancellationToken ct)
        {
            PagesOpened++;
            var parts = url["fake://".Length..].Split('/');
            var key = (parts[0], int.Parse(parts[1]));
            if (ChallengeOn == key) return Task.FromResult(ResultsPage.Of(ResultsState.Challenge, url));
            return Task.FromResult(Pages.TryGetValue(key, out var cards)
                ? new ResultsPage(ResultsState.Results, cards, url)
                : ResultsPage.Of(ResultsState.NoResults, url));
        }

        public Task<ListingDetails?> OpenListingAsync(ListingCard card, CancellationToken ct)
        {
            DetailsOpened.Add(card.ExternalId);
            OnDetails?.Invoke(card.ExternalId);
            if (card.ExternalId == ChallengeOnDetails) throw new BoardChallengeException("verify");
            return Task.FromResult<ListingDetails?>(Details.TryGetValue(card.ExternalId, out var d)
                ? d : HuntRunnerTests.Details(card.ExternalId, ["Remote", "Full-time"]));
        }

        public JobPosting ToPosting(ListingCard card, ListingDetails? details, DateTimeOffset now) => LinkedInSearcher.Map(card, details, now);
    }

    private sealed class NullBrowser : IBrowserSurface
    {
        public string CurrentUrl => "";
        public Task NavigateAsync(string url, CancellationToken ct) => Task.CompletedTask;
        public Task<string> EvalAsync(string script, CancellationToken ct) => Task.FromResult("{}");
        public Task InjectFileAsync(string filePath, string elementJs, CancellationToken ct) => Task.CompletedTask;
        public Task ClickAtPointAsync(double x, double y, CancellationToken ct) => Task.CompletedTask;
        public Task TypeTextAsync(string text, CancellationToken ct) => Task.CompletedTask;
        public Task PressEnterAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<double> SetZoomAsync(double factor, CancellationToken ct) => Task.FromResult(1.0);
    }
}
