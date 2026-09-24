using JobHunt.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class ProfileStoreTests
{
    private TestDb db = null!;
    private ProfileStore store = null!;

    [SetUp]
    public void SetUp()
    {
        db = new TestDb();
        store = new ProfileStore(db, new TestClock(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero)), NullLogger<ProfileStore>.Instance);
    }

    [TearDown]
    public void TearDown() => db.Dispose();

    internal static UserProfile Sample(string first = "Renée", string last = "Okafor") => new()
    {
        Contact = new ContactInfo
        {
            FirstName = first, LastName = last, Email = $"{first.ToLowerInvariant()}@example.com", Phone = "555-0100",
            Address = new Address { City = "Madison", State = "WI", PostalCode = "53703" },
        },
        Authorization = { RequiresSponsorship = false, SecurityClearance = "" },
        Compensation = { MinimumAnnualSalary = 130_000 },
        Preferences = { DesiredTitles = [".NET Developer", "Full Stack Engineer"], WantsRemote = true },
        Summary = { Headline = "Senior .NET engineer", TotalYearsExperience = 12 },
        BoardAccounts = [new() { BoardId = "linkedin", SignInEmail = $"{first.ToLowerInvariant()}@example.com" }],
        Links = [new() { Label = "GitHub", Url = "https://github.com/example" }],
        Experience =
        [
            new()
            {
                Employer = "Acme", Title = "Senior Engineer", StartDate = "2019-04",
                Technologies = ["C#", ".NET", "Azure"],
                Bullets = [new() { Text = "Cut API latency 40% by moving hot paths to Redis", Keywords = ["performance"] },
                           new() { Text = "Mentored four engineers" }],
            },
        ],
        Education = [new() { School = "UW–Madison", Degree = "BS", FieldOfStudy = "Computer Science" }],
        Skills = [new() { Name = "C#", Kind = SkillKind.Hard, Years = 12 }, new() { Name = "Mentoring", Kind = SkillKind.Soft }],
        References = [new() { Name = "Dana Ruiz", Relationship = "Former manager", Company = "Acme" }],
        KeywordLists = [new() { Name = "Domains", Keywords = ["healthcare", "fintech"] }],
        TextBlocks = [new() { Title = "Why I'm looking", Body = "I want to build developer tools." }],
        CustomFields = [new() { Label = "Preferred interview days", Value = "Tue–Thu" }],
        Accomplishments = [new() { Kind = AccomplishmentKind.Award, Title = "Engineer of the Year" }],
        Answers = [new() { Question = "How many years of C# experience do you have?", Answer = "12", Confirmed = true }],
    };

    [Test]
    public async Task SaveThenLoad_RoundTripsEverySection()
    {
        var saved = await store.SaveAsync(Sample());
        var loaded = (await store.LoadAsync(saved.Id))!;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.FullName, Is.EqualTo("Renée Okafor"));
            Assert.That(loaded.Contact.Address.City, Is.EqualTo("Madison"));
            Assert.That(loaded.Compensation.MinimumAnnualSalary, Is.EqualTo(130_000));
            Assert.That(loaded.Preferences.DesiredTitles, Is.EqualTo(new[] { ".NET Developer", "Full Stack Engineer" }));
            Assert.That(loaded.BoardAccounts.Single().SignInEmail, Is.EqualTo("renée@example.com"));
            Assert.That(loaded.Experience.Single().Bullets, Has.Count.EqualTo(2));
            Assert.That(loaded.Experience.Single().Technologies, Does.Contain("Azure"));
            Assert.That(loaded.Skills.Select(s => s.Kind), Is.EquivalentTo(new[] { SkillKind.Hard, SkillKind.Soft }));
            Assert.That(loaded.References.Single().Name, Is.EqualTo("Dana Ruiz"));
            Assert.That(loaded.KeywordLists.Single().Keywords, Does.Contain("fintech"));
            Assert.That(loaded.TextBlocks.Single().Body, Does.StartWith("I want"));
            Assert.That(loaded.CustomFields.Single().Value, Is.EqualTo("Tue–Thu"));
            Assert.That(loaded.Accomplishments.Single().Kind, Is.EqualTo(AccomplishmentKind.Award));
        });
    }

    [Test]
    public async Task Save_EditKeepsFactIdsOfUnchangedRows_AndDeletesRemovedOnes()
    {
        var saved = await store.SaveAsync(Sample());
        var edited = (await store.LoadAsync(saved.Id))!;
        var keptBullet = edited.Experience[0].Bullets[0];
        var keptId = keptBullet.Id;
        keptBullet.Text = "Cut API latency 45%";
        edited.Experience[0].Bullets.RemoveAt(1);
        edited.Experience[0].Bullets.Add(new Bullet { Text = "Led the .NET 8 migration" });

        await store.SaveAsync(edited);
        var bullets = (await store.LoadAsync(saved.Id))!.Experience[0].Bullets;

        Assert.Multiple(() =>
        {
            Assert.That(bullets, Has.Count.EqualTo(2));
            Assert.That(bullets.Single(b => b.Id == keptId).Text, Is.EqualTo("Cut API latency 45%"), "same fact, same id");
            Assert.That(bullets.Any(b => b.Text.StartsWith("Mentored")), Is.False);
            Assert.That(bullets.Any(b => b.Text.Contains(".NET 8")), Is.True);
        });
    }

    [Test]
    public async Task Save_EditToValueGroups_IsPersisted()
    {
        var saved = await store.SaveAsync(Sample());
        var edited = (await store.LoadAsync(saved.Id))!;
        edited.Contact.Address.City = "Milwaukee";
        edited.Compensation.MinimumAnnualSalary = 145_000;
        edited.Preferences.DesiredTitles = [.. edited.Preferences.DesiredTitles, "Staff Engineer"];
        edited.Authorization.RequiresSponsorship = true;

        await store.SaveAsync(edited);
        var reloaded = (await store.LoadAsync(saved.Id))!;

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Contact.Address.City, Is.EqualTo("Milwaukee"));
            Assert.That(reloaded.Compensation.MinimumAnnualSalary, Is.EqualTo(145_000));
            Assert.That(reloaded.Preferences.DesiredTitles, Does.Contain("Staff Engineer"));
            Assert.That(reloaded.Authorization.RequiresSponsorship, Is.True);
        });
    }

    [Test]
    public async Task SeveralUsers_AreListedAndKeptApart()
    {
        var zoe = await store.SaveAsync(Sample("Zoë", "Núñez"));
        var al = await store.SaveAsync(Sample("Al", "Baker"));

        var list = await store.ListAsync();
        var zoeLoaded = (await store.LoadAsync(zoe.Id))!;

        Assert.Multiple(() =>
        {
            Assert.That(list.Select(u => u.DisplayName), Is.EqualTo(new[] { "Al Baker", "Zoë Núñez" }), "alphabetical");
            Assert.That(zoeLoaded.Experience, Has.Count.EqualTo(1), "each user has only their own history");
            Assert.That(zoeLoaded.FullName, Is.EqualTo("Zoë Núñez"));
            Assert.That(al.Id, Is.Not.EqualTo(zoe.Id));
        });
    }

    [Test]
    public async Task Delete_HidesTheUser_AndRestoreBringsThemBack()
    {
        var user = await store.SaveAsync(Sample());
        await store.DeleteAsync(user.Id);

        Assert.That(await store.ListAsync(), Is.Empty);
        Assert.That((await store.ListAsync(includeDeleted: true)).Single().IsDeleted, Is.True);
        Assert.That((await store.LoadAsync(user.Id))!.Experience, Has.Count.EqualTo(1), "nothing was erased");

        await store.RestoreAsync(user.Id);
        Assert.That((await store.ListAsync()).Single().Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Answers_AreFoundByNormalizedQuestion()
    {
        var saved = await store.SaveAsync(Sample());
        var loaded = (await store.LoadAsync(saved.Id))!;
        Assert.That(loaded.FindAnswer("  How many years of C# experience do you have?*  ")?.Answer, Is.EqualTo("12"));
    }

    [Test]
    public async Task ExportThenImport_AddsTheProfileAsANewUser()
    {
        var saved = await store.SaveAsync(Sample());
        var json = await store.ExportAsync(saved.Id);

        Assert.That(json, Does.Not.Contain("\"id\""));
        Assert.That(json, Does.Contain("Renée"), "accented text survives export");
        Assert.That(json, Does.Contain("\"format\": \"jobhunt-profile\""));

        var imported = await store.ImportAsync(json);
        var loaded = (await store.LoadAsync(imported.Id))!;

        Assert.Multiple(async () =>
        {
            Assert.That(imported.Id, Is.Not.EqualTo(saved.Id), "an import never overwrites a user");
            Assert.That(await store.ListAsync(), Has.Count.EqualTo(2));
            Assert.That(loaded.FullName, Is.EqualTo("Renée Okafor"));
            Assert.That(loaded.Experience.Single().Bullets, Has.Count.EqualTo(2));
            Assert.That(loaded.BoardAccounts.Single().BoardId, Is.EqualTo("linkedin"));
            Assert.That(loaded.Answers.Single().NormalizedQuestion, Is.EqualTo("how many years of c# experience do you have"));
        });
    }

    [Test]
    public void Import_RejectsAFileThatIsNotAProfile()
    {
        Assert.Throws<FormatException>(() => ProfileTransfer.Import("{\"hello\":1}"));
        Assert.Throws<FormatException>(() => ProfileTransfer.Import("not json"));
    }

    [Test]
    public async Task ANewJobEntryCarryingSomeoneElsesBulletIds_NeverTouchesTheirBullets()
    {
        var ann = await store.SaveAsync(Sample("Ann", "Lee"));
        var annBulletIds = ann.Experience[0].Bullets.Select(b => b.Id).ToList();
        var bob = await store.SaveAsync(Sample("Bob", "Kay"));

        var edited = (await store.LoadAsync(bob.Id))!;
        edited.Experience.Add(new WorkExperience
        {
            Employer = "Initech", Title = "Dev",
            Bullets = annBulletIds.Select(id => new Bullet { Id = id, Text = "HIJACK" }).ToList(),
        });
        await store.SaveAsync(edited);

        var annAfter = (await store.LoadAsync(ann.Id))!;
        var bobAfter = (await store.LoadAsync(bob.Id))!;
        Assert.Multiple(() =>
        {
            Assert.That(annAfter.Experience[0].Bullets.Select(b => b.Text), Has.None.EqualTo("HIJACK"));
            Assert.That(annAfter.Experience[0].Bullets.Select(b => b.Id), Is.EquivalentTo(annBulletIds));
            Assert.That(bobAfter.Experience.Single(e => e.Employer == "Initech").Bullets, Has.Count.EqualTo(annBulletIds.Count));
        });
    }

    [Test]
    public async Task DuplicatingAJobEntry_SavesACopy()
    {
        var saved = await store.SaveAsync(Sample());
        var edited = (await store.LoadAsync(saved.Id))!;
        var original = edited.Experience[0];
        edited.Experience.Add(new WorkExperience
        {
            Id = original.Id, Employer = original.Employer + " (copy)", Title = original.Title,
            Bullets = original.Bullets.Select(b => new Bullet { Id = b.Id, Text = b.Text }).ToList(),
        });

        await store.SaveAsync(edited);
        var reloaded = (await store.LoadAsync(saved.Id))!;
        Assert.That(reloaded.Experience, Has.Count.EqualTo(2));
        Assert.That(reloaded.Experience.Sum(e => e.Bullets.Count), Is.EqualTo(4));
    }

    [Test]
    public async Task ReorderingAList_IsSaved()
    {
        var p = Sample();
        p.Experience.Insert(0, new WorkExperience { Employer = "NewCo", Title = "Lead" });
        var saved = await store.SaveAsync(p);
        var edited = (await store.LoadAsync(saved.Id))!;
        Assert.That(edited.Experience.Select(e => e.Employer), Is.EqualTo(new[] { "NewCo", "Acme" }), "the order it was entered in");

        edited.Experience.Reverse();
        edited.Skills.Reverse();
        await store.SaveAsync(edited);
        var reloaded = (await store.LoadAsync(saved.Id))!;
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Experience.Select(e => e.Employer), Is.EqualTo(new[] { "Acme", "NewCo" }));
            Assert.That(reloaded.Skills.Select(s => s.Name), Is.EqualTo(new[] { "Mentoring", "C#" }));
        });
    }
}
