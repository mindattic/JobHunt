using JobHunt.Core.Boards;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Jobs;

namespace JobHunt.Tests;

[TestFixture]
public class LinkedInTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [TestCase("2 weeks ago", -14 * 24)]
    [TestCase("Reposted 3 days ago", -3 * 24)]
    [TestCase("1 hour ago", -1)]
    [TestCase("Posted 5 hours ago", -5)]
    [TestCase("1 day ago", -24)]
    public void PostedAgo(string text, int hoursAgo) =>
        Assert.That(LinkedInText.ParsePostedAgo(text, Now), Is.EqualTo(Now.AddHours(hoursAgo)));

    [Test]
    public void PostedAgo_MinutesMonthsAndJustNow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LinkedInText.ParsePostedAgo("30 minutes ago", Now), Is.EqualTo(Now.AddMinutes(-30)));
            Assert.That(LinkedInText.ParsePostedAgo("1 month ago", Now), Is.EqualTo(Now.AddMonths(-1)));
            Assert.That(LinkedInText.ParsePostedAgo("Just now", Now), Is.EqualTo(Now));
            Assert.That(LinkedInText.ParsePostedAgo("Chicago, IL", Now), Is.Null);
        });
    }

    [TestCase("57 applicants", 57)]
    [TestCase("Over 100 applicants", 100)]
    [TestCase("Be among the first 25 applicants", 25)]
    [TestCase("1,203 applicants", 1203)]
    [TestCase("12 people clicked apply", 12)]
    public void Applicants(string text, int expected) => Assert.That(LinkedInText.ParseApplicants(text), Is.EqualTo(expected));

    [Test]
    public void Applicants_NotStated() => Assert.That(LinkedInText.ParseApplicants("Remote"), Is.Null);

    [TestCase("$120K/yr - $150K/yr", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$120,000/yr - $150,000/yr", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$60/hr - $75/hr", 60, 75, PayPeriod.Hour)]
    [TestCase("$95K/yr", 95_000, 95_000, PayPeriod.Year)]
    [TestCase("$120K – $150K", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$65 - $80 an hour, per hour", 65, 80, PayPeriod.Hour)]
    [TestCase("$8,000/mo", 8_000, 8_000, PayPeriod.Month)]
    public void Salary(string text, double min, double max, PayPeriod period)
    {
        var (lo, hi, p) = LinkedInText.ParseSalary(text);
        Assert.That((lo, hi, p), Is.EqualTo(((decimal?)min, (decimal?)max, (PayPeriod?)period)));
    }

    [TestCase("$120-150K", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$120K-150K", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$120,000 - $150,000 Matches your job preferences", 120_000, 150_000, PayPeriod.Year)]
    [TestCase("$600/day", 600, 600, PayPeriod.Day)]
    [TestCase("$3,000/wk", 3_000, 3_000, PayPeriod.Week)]
    [TestCase("$1.2M", 1_200_000, 1_200_000, PayPeriod.Year)]
    public void Salary_EdgeCases(string text, double min, double max, PayPeriod period)
    {
        var (lo, hi, p) = LinkedInText.ParseSalary(text);
        Assert.That((lo, hi, p), Is.EqualTo(((decimal?)min, (decimal?)max, (PayPeriod?)period)));
    }

    [TestCase("£55K/yr - £65K/yr", "GBP")]
    [TestCase("€4,000 - €5,000 monthly", "EUR")]
    [TestCase("$120K/yr", "USD")]
    public void Salary_Currency(string text, string currency) => Assert.That(LinkedInText.ParsePay(text).Currency, Is.EqualTo(currency));

    [Test]
    public void Salary_EuroMonthly_IsNotBillions()
    {
        var (lo, hi, p) = LinkedInText.ParseSalary("€4,000 - €5,000 monthly");
        Assert.That((lo, hi, p), Is.EqualTo(((decimal?)4_000m, (decimal?)5_000m, (PayPeriod?)PayPeriod.Month)));
    }

    [Test]
    public void PostedAgo_PlusAndArticle()
    {
        Assert.That(LinkedInText.ParsePostedAgo("30+ days ago", Now), Is.EqualTo(Now.AddDays(-30)));
        Assert.That(LinkedInText.ParsePostedAgo("a week ago", Now), Is.EqualTo(Now.AddDays(-7)));
        Assert.That(LinkedInText.ParsePostedAgo("an hour ago", Now), Is.EqualTo(Now.AddHours(-1)));
    }

    [Test]
    public void Applicants_CompareToOthers() =>
        Assert.That(LinkedInText.ParseApplicants("See how you compare to 87 other applicants"), Is.EqualTo(87));

    [Test]
    public void Salary_NotStated() => Assert.That(LinkedInText.ParseSalary("Full-time").Max, Is.Null);

    [Test]
    public void WorkplaceAndType_FromTags()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LinkedInText.ParseWorkplace(["$120K/yr", "Remote", "Full-time"]), Is.EqualTo(WorkplaceType.Remote));
            Assert.That(LinkedInText.ParseWorkplace(["On-site"]), Is.EqualTo(WorkplaceType.OnSite));
            Assert.That(LinkedInText.ParseWorkplace(["Hybrid"]), Is.EqualTo(WorkplaceType.Hybrid));
            Assert.That(LinkedInText.ParseEmploymentType(["Remote", "Contract"]), Is.EqualTo(EmploymentType.Contract));
            Assert.That(LinkedInText.ParseEmploymentType(["Full-time", "Mid-Senior level"]), Is.EqualTo(EmploymentType.FullTime));
            Assert.That(LinkedInText.ParseEmploymentType(["Mid-Senior level"]), Is.EqualTo(EmploymentType.Unknown));
        });
    }

    [TestCase("https://www.linkedin.com/jobs/view/4012345678/", "4012345678")]
    [TestCase("https://www.linkedin.com/jobs/view/senior-net-developer-at-acme-4012345678?refId=x", "4012345678")]
    [TestCase("https://www.linkedin.com/jobs/search/?currentJobId=4012345678&keywords=.net", "4012345678")]
    [TestCase("https://www.linkedin.com/feed/", null)]
    public void JobIdFromUrl(string url, string? id) => Assert.That(LinkedInText.JobIdFromUrl(url), Is.EqualTo(id));

    [Test]
    public void Undouble_RemovesTheScreenReaderRepeat()
    {
        Assert.That(LinkedInText.Undouble("Senior .NET Developer Senior .NET Developer"), Is.EqualTo("Senior .NET Developer"));
        Assert.That(LinkedInText.Undouble("  Acme   Health "), Is.EqualTo("Acme Health"));
    }

    [Test]
    public void Map_TurnsWhatWasReadIntoAPosting()
    {
        var card = new ListingCard("4012345678", "Senior .NET Developer", "Acme", "United States (Remote)", QuickApply: true, Applied: false, Promoted: false);
        var details = new ListingDetails("4012345678", "Senior .NET Developer", "Acme Health", "United States",
            ["United States", "2 days ago", "Over 100 applicants"], ["$140K/yr - $165K/yr", "Remote", "Full-time", "Mid-Senior level"],
            "About the job\nBuild billing APIs in C#.", "easy");

        var p = LinkedInSearcher.Map(card, details, Now);

        Assert.Multiple(() =>
        {
            Assert.That(p.BoardId, Is.EqualTo("linkedin"));
            Assert.That(p.Url, Is.EqualTo("https://www.linkedin.com/jobs/view/4012345678/"));
            Assert.That(p.Company, Is.EqualTo("Acme Health"), "the details pane wins over the card");
            Assert.That(p.SupportsQuickApply, Is.True);
            Assert.That(p.PostedAt, Is.EqualTo(Now.AddDays(-2)));
            Assert.That(p.ApplicantCount, Is.EqualTo(100));
            Assert.That(p.Workplace, Is.EqualTo(WorkplaceType.Remote));
            Assert.That(p.EmploymentType, Is.EqualTo(EmploymentType.FullTime));
            Assert.That((p.SalaryMin, p.SalaryMax, p.SalaryPeriod), Is.EqualTo(((decimal?)140_000m, (decimal?)165_000m, (PayPeriod?)PayPeriod.Year)));
            Assert.That(p.RawDescription, Does.Contain("billing APIs"));
        });
    }

    [Test]
    public void Map_ExternalApply_IsNotQuickApply_AndAppliedIsSeen()
    {
        var card = new ListingCard("1", "T", "C", "L", QuickApply: false, Applied: false, Promoted: false);
        Assert.That(LinkedInSearcher.Map(card, Details("external"), Now).SupportsQuickApply, Is.False);
        Assert.That(LinkedInSearcher.Map(card, Details("applied"), Now).BoardShowsApplied, Is.True);
    }

    private static ListingDetails Details(string apply) => new("1", "T", "C", "L", [], [], "d", apply);
}
