using JobHunt.Core.Applications;
using JobHunt.Core.Data;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class DatabaseTransferTests
{
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

    private static async Task Seed(TestDb db)
    {
        var profiles = new ProfileStore(db, Clock, NullLogger<ProfileStore>.Instance);
        var renee = await profiles.SaveAsync(ProfileStoreTests.Sample());
        var al = await profiles.SaveAsync(ProfileStoreTests.Sample("Al", "Baker"));
        await profiles.DeleteAsync(al.Id);

        var jobs = new JobStore(db, Clock, NullLogger<JobStore>.Instance);
        var (job, _) = await jobs.UpsertFoundAsync(renee.Id, new JobPosting
        {
            BoardId = "linkedin", ExternalId = "4012345678", Title = "Senior .NET Developer", Company = "Acme",
            Url = "https://www.linkedin.com/jobs/view/4012345678/", Workplace = WorkplaceType.Remote,
            EmploymentType = EmploymentType.Contract, ContractMonths = 6, SalaryMin = 70, SalaryMax = 85,
            SalaryPeriod = PayPeriod.Hour, PlainDescription = "Build billing APIs in C#.",
            Requirements = [new() { Text = "C#", Kind = RequirementKind.Required, MatchedFactRefs = ["skill:1"] }],
            Evidence = { ["employmentType"] = "This is a 6-month contract." },
        }, ".NET Developer");
        var app = await jobs.RecordApplicationAsync(new JobApplication { JobPostingId = job.Id, Method = "LinkedIn Easy Apply", RunLog = ["Submitted"] });
        await jobs.RecordOutcomeAsync(app.Id, ApplicationOutcome.Interviewing, OutcomeSource.Manual, "Phone screen");
        await jobs.UpsertFoundAsync(al.Id, new JobPosting { BoardId = "linkedin", ExternalId = "999", Title = "Baker" }, "s");

        await using var ctx = db.CreateDbContext();
        ctx.Searches.Add(new SearchProfile { UserProfileId = renee.Id, Name = "Remote .NET", SearchTerms = [".NET Developer", "C# Engineer"], Filters = { MinAnnualSalary = 140_000 } });
        ctx.Settings.Add(new AppSettings { SelectedLlmProvider = "gemini", DryRun = false, DailyApplyCap = 10, ActiveProfileId = renee.Id });
        await ctx.SaveChangesAsync();
    }

    [Test]
    public async Task ExportThenImport_IntoAnotherDatabase_RestoresEveryUser()
    {
        using var source = new TestDb();
        await Seed(source);
        var json = await new DatabaseTransfer(source, Clock, NullLogger<DatabaseTransfer>.Instance).ExportAsync();

        Assert.That(json, Does.Contain("\"format\": \"jobhunt-backup\""));
        Assert.That(json, Does.Not.Contain("\"id\":"), "database ids are never exported");

        using var target = new TestDb();
        await new DatabaseTransfer(target, Clock, NullLogger<DatabaseTransfer>.Instance).ImportAsync(json);

        await using var db = target.CreateDbContext();
        var profiles = new ProfileStore(target, Clock, NullLogger<ProfileStore>.Instance);
        var users = await profiles.ListAsync(includeDeleted: true);
        var renee = users.Single(u => u.DisplayName == "Renée Okafor");
        var job = await db.Jobs.Include(j => j.Applications).ThenInclude(a => a.Events).SingleAsync(j => j.UserProfileId == renee.Id);
        var app = job.Applications.Single();
        var settings = await db.Settings.SingleAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(users, Has.Count.EqualTo(2));
            Assert.That(users.Single(u => u.DisplayName == "Al Baker").IsDeleted, Is.True, "a deleted user stays deleted");
            Assert.That((await profiles.LoadAsync(renee.Id))!.Experience.Single().Bullets, Has.Count.EqualTo(2));
            Assert.That(job.SalaryMax, Is.EqualTo(85m));
            Assert.That(job.ContractMonths, Is.EqualTo(6));
            Assert.That(job.Requirements.Single().MatchedFactRefs, Is.Empty, "fact ids change on import, so old matches are dropped");
            Assert.That(job.UnderstoodAt, Is.Null, "and the job is marked for re-analysis");
            Assert.That(job.Evidence["employmentType"], Does.Contain("6-month"));
            Assert.That(app.Outcome, Is.EqualTo(ApplicationOutcome.Interviewing));
            Assert.That(app.Events, Has.Count.EqualTo(2));
            Assert.That((await db.Searches.SingleAsync()).UserProfileId, Is.EqualTo(renee.Id));
            Assert.That(await db.Jobs.CountAsync(), Is.EqualTo(2), "each user's jobs came back to that user");
            Assert.That(settings.SelectedLlmProvider, Is.EqualTo("gemini"));
            Assert.That(settings.ActiveProfileId, Is.EqualTo(renee.Id), "the first user who isn't deleted is active");
        });
    }

    [Test]
    public async Task Import_ReplacesWhatWasThere()
    {
        using var db = new TestDb();
        await Seed(db);
        var transfer = new DatabaseTransfer(db, Clock, NullLogger<DatabaseTransfer>.Instance);
        var json = await transfer.ExportAsync();

        await transfer.ImportAsync(json);   // importing the same backup again must not duplicate

        await using var ctx = db.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(ctx.Profiles.Count(), Is.EqualTo(2));
            Assert.That(ctx.Jobs.Count(), Is.EqualTo(2));
            Assert.That(ctx.Applications.Count(), Is.EqualTo(1));
            Assert.That(ctx.Set<Bullet>().Count(), Is.EqualTo(4), "old profile children were removed");
            Assert.That(ctx.Settings.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void Parse_RejectsOtherFiles()
    {
        Assert.Throws<FormatException>(() => DatabaseTransfer.Parse("{\"format\":\"jobhunt-profile\",\"version\":1}"));
        Assert.Throws<FormatException>(() => DatabaseTransfer.Parse("[]"));
    }
}
