using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Scoring;

namespace JobHunt.Tests;

[TestFixture]
public class FitScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private readonly FitScorer scorer = new();

    private static SearchProfile Search(Action<HuntFilters>? tweak = null)
    {
        var s = new SearchProfile { SearchTerms = [".NET Developer", "Full Stack Engineer"] };
        s.Filters.MinAnnualSalary = 130_000;
        tweak?.Invoke(s.Filters);
        return s;
    }

    private static JobPosting Strong() => new()
    {
        Title = "Senior .NET Developer", Company = "Acme", SupportsQuickApply = true,
        Workplace = WorkplaceType.Remote, EmploymentType = EmploymentType.FullTime,
        SalaryMin = 150_000, SalaryMax = 170_000, SalaryPeriod = PayPeriod.Year,
        PostedAt = Now.AddDays(-1), ApplicantCount = 12,
        Requirements =
        [
            new() { Text = "5+ years C#", Kind = RequirementKind.Required, MatchedFactRefs = ["skill:1"] },
            new() { Text = "ASP.NET Core", Kind = RequirementKind.Required, MatchedFactRefs = ["bullet:3"] },
            new() { Text = "Kubernetes", Kind = RequirementKind.NiceToHave },
        ],
    };

    [Test]
    public void StrongMatch_PassesAndScoresHigh_WithAnExplainedBreakdown()
    {
        var result = scorer.Evaluate(Strong(), Search(), null, Now);
        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Score, Is.GreaterThanOrEqualTo(85));
            Assert.That(result.Breakdown.Single(c => c.Criterion == "Required skills").Detail, Is.EqualTo("Matched 2/2"));
            Assert.That(result.Breakdown.Single(c => c.Criterion == "Nice-to-have skills").Detail, Is.EqualTo("Matched 0/1"));
            Assert.That(result.Breakdown.Single(c => c.Criterion == "Salary").Detail, Does.Contain("$170k"));
        });
    }

    [Test]
    public void OnSiteJob_FailsTheDefaultRemoteOnlyFilter()
    {
        var job = Strong();
        job.Workplace = WorkplaceType.OnSite;
        var result = scorer.Evaluate(job, Search(), null, Now);
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Rejections, Has.One.Contains("OnSite"));
    }

    [Test]
    public void UnknownSalary_IsKeptWithHalfCredit_UnlessExcluded()
    {
        var job = Strong();
        job.SalaryMin = job.SalaryMax = null;
        job.SalaryPeriod = null;

        var kept = scorer.Evaluate(job, Search(), null, Now);
        var salary = kept.Breakdown.Single(c => c.Criterion == "Salary");
        Assert.That(kept.Passed, Is.True);
        Assert.That(salary.Earned, Is.EqualTo(salary.Weight / 2));

        Assert.That(scorer.Evaluate(job, Search(f => f.ExcludeUnknownSalary = true), null, Now).Passed, Is.False);
    }

    [Test]
    public void HourlyRate_IsAnnualizedBeforeComparing()
    {
        var job = Strong();
        job.EmploymentType = EmploymentType.Contract;
        job.SalaryMin = 55; job.SalaryMax = 60; job.SalaryPeriod = PayPeriod.Hour;   // $124,800/yr
        var result = scorer.Evaluate(job, Search(), null, Now);
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Rejections.Single(), Does.Contain("$125k"));

        job.SalaryMax = 70;   // $145,600/yr
        Assert.That(scorer.Evaluate(job, Search(), null, Now).Passed, Is.True);
    }

    [Test]
    public void ShortContract_FailsTheMinimumDuration()
    {
        var job = Strong();
        job.EmploymentType = EmploymentType.Contract;
        job.ContractMonths = 3;
        Assert.That(scorer.Evaluate(job, Search(f => f.MinContractMonths = 6), null, Now).Passed, Is.False);
        job.ContractMonths = 12;
        Assert.That(scorer.Evaluate(job, Search(f => f.MinContractMonths = 6), null, Now).Passed, Is.True);
    }

    [Test]
    public void ProfileFacts_DriveSponsorshipAndClearanceFilters()
    {
        var job = Strong();
        job.SponsorshipAvailable = false;
        job.ClearanceRequired = true;
        var profile = new UserProfile { Authorization = { RequiresSponsorship = true } };
        var result = scorer.Evaluate(job, Search(), profile, Now);
        Assert.That(result.Rejections, Has.Count.EqualTo(2));
    }

    [Test]
    public void AvoidedTech_AndBlockedCompanies_AreRejected()
    {
        var job = Strong();
        job.TechStack = ["PHP", "MySQL"];
        var result = scorer.Evaluate(job, Search(f => { f.MustAvoidTech = ["php"]; f.CompanyBlocklist = ["ACME"]; }), null, Now);
        Assert.That(result.Rejections, Has.Count.EqualTo(2));
    }

    [Test]
    public void UnstatedFields_NeverFailAFilter()
    {
        var bare = new JobPosting { Title = "Engineer", SupportsQuickApply = true };
        Assert.That(scorer.Evaluate(bare, Search(), null, Now).Passed, Is.True);
    }

    [Test]
    public void TitleTokens_KeepDotNetAndCSharpIntact()
    {
        var tokens = FitScorer.Tokens("Sr. .NET / C# Developer (Remote)");
        Assert.That(tokens, Is.SupersetOf(new[] { ".net", "c#", "developer", "remote" }));
    }
}
