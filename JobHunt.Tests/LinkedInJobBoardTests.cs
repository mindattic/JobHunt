using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;

namespace JobHunt.Tests;

[TestFixture]
public class LinkedInJobBoardTests
{
    private readonly LinkedInJobBoard board = new();

    private static Dictionary<string, string> Query(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Test]
    public void Defaults_AreRemoteEasyApplyPastWeekInTheUS()
    {
        var url = board.BuildSearchUrl(".NET Developer", new HuntFilters());
        var q = Query(url);

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.StartWith("https://www.linkedin.com/jobs/search/?"));
            Assert.That(q["keywords"], Is.EqualTo(".NET Developer"));
            Assert.That(q["f_WT"], Is.EqualTo("2"), "remote");
            Assert.That(q["f_AL"], Is.EqualTo("true"), "Easy Apply only");
            Assert.That(q["f_TPR"], Is.EqualTo("r604800"), "past week");
            Assert.That(q["f_JT"], Is.EqualTo("F,C"), "full-time + contract (contract-to-hire folds into C)");
            Assert.That(q["geoId"], Is.EqualTo(LinkedInJobBoard.UnitedStatesGeoId));
            Assert.That(q["sortBy"], Is.EqualTo("DD"));
            Assert.That(q.ContainsKey("start"), Is.False);
        });
    }

    [Test]
    public void EveryFilter_MapsToItsParameter()
    {
        var filters = new HuntFilters
        {
            Location = "Chicago, IL", DistanceMiles = 25,
            Workplaces = [WorkplaceType.Hybrid, WorkplaceType.OnSite],
            EmploymentTypes = [EmploymentType.PartTime, EmploymentType.Temporary, EmploymentType.Internship],
            ExperienceLevels = [ExperienceLevel.MidSenior, ExperienceLevel.Director],
            DatePosted = DatePosted.PastDay, MinAnnualSalary = 130_000, UnderTenApplicants = true,
            Sort = SortOrder.MostRelevant, BoardOptions = { ["linkedin.geoId"] = "103112676" },
        };
        var q = Query(board.BuildSearchUrl("C#", filters, page: 2));

        Assert.Multiple(() =>
        {
            Assert.That(q["keywords"], Is.EqualTo("C#"), "the # is escaped, not treated as a fragment");
            Assert.That(q["location"], Is.EqualTo("Chicago, IL"));
            Assert.That(q["geoId"], Is.EqualTo("103112676"));
            Assert.That(q["distance"], Is.EqualTo("25"));
            Assert.That(q["f_WT"], Is.EqualTo("3,1"));
            Assert.That(q["f_JT"], Is.EqualTo("P,T,I"));
            Assert.That(q["f_E"], Is.EqualTo("4,5"));
            Assert.That(q["f_TPR"], Is.EqualTo("r86400"));
            Assert.That(q["f_SB2"], Is.EqualTo("5"), "$130k rounds down to the $120k+ bucket");
            Assert.That(q["f_EA"], Is.EqualTo("true"));
            Assert.That(q["sortBy"], Is.EqualTo("R"));
            Assert.That(q["start"], Is.EqualTo("50"));
        });
    }

    [TestCase(null, null)]
    [TestCase(35_000, null)]
    [TestCase(40_000, 1)]
    [TestCase(119_999, 4)]
    [TestCase(120_000, 5)]
    [TestCase(250_000, 9)]
    public void SalaryBucket_RoundsDownSoNoQualifyingJobIsLost(int? min, int? expected) =>
        Assert.That(LinkedInJobBoard.SalaryBucketFor(min), Is.EqualTo(expected));

    [Test]
    public void QuickApplyOff_OmitsTheEasyApplyFilter()
    {
        var q = Query(board.BuildSearchUrl("x", new HuntFilters { QuickApplyOnly = false, Workplaces = [] }));
        Assert.That(q.ContainsKey("f_AL") || q.ContainsKey("f_WT"), Is.False);
    }
}
