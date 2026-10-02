using JobHunt.Core.Boards.Indeed;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;

namespace JobHunt.Tests;

[TestFixture]
public class IndeedJobBoardTests
{
    private readonly IndeedJobBoard board = new();

    private static Dictionary<string, string> Query(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Test]
    public void Id_IsStableAndLowercase() => Assert.That(board.Id, Is.EqualTo("indeed"));

    [Test]
    public void Defaults_MapKeywordsAndPastWeek()
    {
        var url = board.BuildSearchUrl(".NET Developer", new HuntFilters());
        var q = Query(url);

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.StartWith("https://www.indeed.com/jobs?"));
            Assert.That(q["q"], Is.EqualTo(".NET Developer"));
            Assert.That(q["fromage"], Is.EqualTo("7"), "past week");
            Assert.That(q.ContainsKey("start"), Is.False);
        });
    }

    [Test]
    public void RemoteOnly_WithNoExplicitLocation_SearchesRemote()
    {
        var q = Query(board.BuildSearchUrl("C#", new HuntFilters { Workplaces = [WorkplaceType.Remote], Location = "United States" }));
        Assert.That(q["l"], Is.EqualTo("Remote"));
    }

    [Test]
    public void ExplicitBoardOption_OverridesTheRemoteLocationGuess()
    {
        var filters = new HuntFilters
        {
            Workplaces = [WorkplaceType.Remote],
            BoardOptions = { ["indeed.location"] = "Chicago, IL" },
        };
        var q = Query(board.BuildSearchUrl("C#", filters));
        Assert.That(q["l"], Is.EqualTo("Chicago, IL"));
    }

    [Test]
    public void EveryFilter_MapsToItsParameter()
    {
        var filters = new HuntFilters
        {
            Location = "Chicago, IL", DistanceMiles = 25,
            EmploymentTypes = [EmploymentType.Contract],
            DatePosted = DatePosted.PastDay,
            Sort = SortOrder.MostRecent,
        };
        var q = Query(board.BuildSearchUrl("C#", filters, page: 2));

        Assert.Multiple(() =>
        {
            Assert.That(q["q"], Is.EqualTo("C#"), "the # is escaped, not treated as a fragment");
            Assert.That(q["l"], Is.EqualTo("Chicago, IL"));
            Assert.That(q["radius"], Is.EqualTo("25"));
            Assert.That(q["jt"], Is.EqualTo("contract"));
            Assert.That(q["fromage"], Is.EqualTo("1"));
            Assert.That(q["sort"], Is.EqualTo("date"));
            Assert.That(q["start"], Is.EqualTo("20"));
        });
    }

    [Test]
    public void SessionCookie_IsTheAccountCookie_NotTheVisitorTrackingOne() =>
        Assert.That(board.SessionCookieName, Is.EqualTo("PPID"));

    /// <summary>The exact values tools/verify-indeed.mjs reads off the live-captured fixture.</summary>
    [Test]
    public void Map_ReadsPayWorkplaceAndTypeFromTheLivePaneShape()
    {
        var card = new JobHunt.Core.Boards.ListingCard("658def0d84fa7505", "Senior Software Engineer", "Thyme Care", "Remote",
            QuickApply: true, Applied: false, Promoted: false);
        var details = new JobHunt.Core.Boards.ListingDetails("658def0d84fa7505", "Senior Software Engineer", "Thyme Care", "Remote",
            TopCardFacts: ["Remote", "$175,000 - $200,000 a year", "Full-time"],
            Insights: ["$175,000 - $200,000 a year", "Full-time", "On call", "Remote"],
            Description: "OUR MISSION ...", ApplyKind: "easy");

        var job = IndeedSearcher.Map(card, details, DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(job.BoardId, Is.EqualTo("indeed"));
            Assert.That(job.Url, Is.EqualTo("https://www.indeed.com/viewjob?jk=658def0d84fa7505"));
            Assert.That(job.SalaryMin, Is.EqualTo(175_000m));
            Assert.That(job.SalaryMax, Is.EqualTo(200_000m));
            Assert.That(job.SalaryPeriod, Is.EqualTo(PayPeriod.Year));
            Assert.That(job.Workplace, Is.EqualTo(WorkplaceType.Remote), "the first pill is the pay; every pill must be checked");
            Assert.That(job.EmploymentType, Is.EqualTo(EmploymentType.FullTime));
            Assert.That(job.SupportsQuickApply, Is.True);
        });
    }

    [TestCase("Hybrid remote in Chicago, IL", WorkplaceType.Hybrid)]
    [TestCase("Remote in Chicago, IL", WorkplaceType.Remote)]
    [TestCase("In person", WorkplaceType.OnSite)]
    [TestCase("Chicago, IL", WorkplaceType.Unknown)]
    public void ParseWorkplace_ChecksHybridBeforeRemote(string text, WorkplaceType expected) =>
        Assert.That(IndeedSearcher.ParseWorkplace([text]), Is.EqualTo(expected));

    [Test]
    public void ContractToHire_FoldsIntoContract_LikeLinkedIn()
    {
        var q = Query(board.BuildSearchUrl("x", new HuntFilters { EmploymentTypes = [EmploymentType.ContractToHire] }));
        Assert.That(q["jt"], Is.EqualTo("contract"));
    }
}
