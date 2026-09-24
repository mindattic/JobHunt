using AutoWebNav;
using JobHunt.Core.Boards;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class BoardPasswordsTests
{
    private string dir = null!;

    [SetUp]
    public void SetUp() => dir = Directory.CreateTempSubdirectory("jobhunt-secrets-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    [Test]
    public void Password_RoundTrips_PerUserAndBoard_AndIsNeverStoredInPlainText()
    {
        var passwords = new BoardPasswords(dir);
        passwords.Set(1, "linkedin", "correct horse battery staple");
        passwords.Set(2, "linkedin", "someone else's");

        var onDisk = string.Concat(Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Multiple(() =>
        {
            Assert.That(new BoardPasswords(dir).Get(1, "linkedin"), Is.EqualTo("correct horse battery staple"));
            Assert.That(passwords.Get(2, "linkedin"), Is.EqualTo("someone else's"));
            Assert.That(passwords.Get(1, "indeed"), Is.Null);
            Assert.That(onDisk, Does.Not.Contain("correct horse"), "encrypted with DPAPI before Vault writes it");
        });
    }

    [Test]
    public void Clear_ForgetsOnlyThatUsersPassword()
    {
        var passwords = new BoardPasswords(dir);
        passwords.Set(1, "linkedin", "a");
        passwords.Set(2, "linkedin", "b");
        passwords.Clear(1, "linkedin");
        Assert.That(passwords.Has(1, "linkedin"), Is.False);
        Assert.That(passwords.Has(2, "linkedin"), Is.True);
    }
}

[TestFixture]
public class ByokKeysTests
{
    [Test]
    public void AddingKeys_AddsToThePool_AndBlankInputChangesNothing()
    {
        var dir = Directory.CreateTempSubdirectory("jobhunt-keys-").FullName;
        try
        {
            var keys = new JobHunt.Core.Llm.ByokKeys(new MindAttic.Vault.Credentials.CredentialStore(dir));
            Assert.That(keys.AddOwnKeys("claude", ["sk-one"]), Is.EqualTo(1));
            Assert.That(keys.AddOwnKeys("claude", ["sk-two", "sk-one", "  "]), Is.EqualTo(1), "duplicates and blanks ignored");
            Assert.That(keys.AddOwnKeys("claude", [""]), Is.Zero);
            Assert.That(keys.GetOwnKeys("claude"), Is.EqualTo(new[] { "sk-one", "sk-two" }), "nothing was lost");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

[TestFixture]
public class LinkedInSignInTests
{
    private static readonly LinkedInSignIn SignIn = new(NullLogger<LinkedInSignIn>.Instance) { PollIntervalMs = 1, TimeoutMs = 50 };

    private static string Page(bool signedIn = false, bool loginForm = false, bool challenge = false, string error = "") =>
        $$"""{"url":"https://www.linkedin.com/x","challenge":{{Lower(challenge)}},"signedIn":{{Lower(signedIn)}},"loginForm":{{Lower(loginForm)}},"error":"{{error}}"}""";

    private static string Lower(bool b) => b ? "true" : "false";

    [Test]
    public async Task AlreadySignedIn_DoesNothing()
    {
        var browser = new ScriptedBrowser(Page(signedIn: true));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "a@b.c", "pw", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.AlreadySignedIn));
        Assert.That(browser.Typed, Is.Empty);
    }

    [Test]
    public async Task NoPassword_LeavesSignInToTheUser()
    {
        var browser = new ScriptedBrowser(Page(loginForm: true));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "a@b.c", null, default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.NoCredentials));
    }

    [Test]
    public async Task LoginForm_IsFilledWithTrustedTyping_AndSubmitted()
    {
        var browser = new ScriptedBrowser(Page(loginForm: true), Page(signedIn: true));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "renee@example.com", "s3cret", default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SignInStatus.SignedIn));
            Assert.That(browser.Typed, Is.EqualTo(new[] { "renee@example.com", "s3cret" }));
            Assert.That(browser.Clicks, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerificationChallenge_StopsAndHandsOver()
    {
        var browser = new ScriptedBrowser(Page(loginForm: true), Page(challenge: true));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "a@b.c", "pw", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.NeedsYou));
    }

    [Test]
    public async Task ChallengeBeforeSignIn_IsNeverTypedInto()
    {
        var browser = new ScriptedBrowser(Page(challenge: true));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "a@b.c", "pw", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.NeedsYou));
        Assert.That(browser.Typed, Is.Empty);
    }

    [Test]
    public async Task WrongPassword_IsReported()
    {
        var browser = new ScriptedBrowser(Page(loginForm: true), Page(loginForm: true, error: "Wrong email or password."));
        var result = await SignIn.EnsureSignedInAsync(browser, "https://www.linkedin.com/login", "a@b.c", "bad", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.Failed));
        Assert.That(result.Message, Does.Contain("Wrong email or password"));
    }

    /// <summary>Answers the page probe with a scripted sequence (the last one repeats); answers
    /// every other script as a found element at (10,10).</summary>
    private sealed class ScriptedBrowser(params string[] probes) : IBrowserSurface
    {
        private int next;
        public List<string> Typed { get; } = [];
        public int Clicks { get; private set; }
        public string CurrentUrl => "https://www.linkedin.com/";

        public Task<string> EvalAsync(string script, CancellationToken ct)
        {
            if (script == LinkedInSignIn.ProbeScript)
                return Task.FromResult(probes[Math.Min(next++, probes.Length - 1)]);
            return Task.FromResult("""{"ok":true,"found":true,"x":10,"y":10}""");
        }

        public Task TypeTextAsync(string text, CancellationToken ct) { Typed.Add(text); return Task.CompletedTask; }
        public Task ClickAtPointAsync(double x, double y, CancellationToken ct) { Clicks++; return Task.CompletedTask; }
        public Task NavigateAsync(string url, CancellationToken ct) => Task.CompletedTask;
        public Task InjectFileAsync(string filePath, string elementJs, CancellationToken ct) => Task.CompletedTask;
        public Task PressEnterAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<double> SetZoomAsync(double factor, CancellationToken ct) => Task.FromResult(1.0);
    }
}

[TestFixture]
public class SearchStoreTests
{
    [Test]
    public void FirstRequirements_ComeFromTheProfilePreferences()
    {
        var user = new UserProfile
        {
            Id = 7,
            Preferences = { DesiredTitles = [".NET Developer", "Full Stack Engineer"], WantsRemote = true, WantsHybrid = true, WantsPartTime = false, CompanyBlocklist = ["Initech"] },
            Compensation = { MinimumAnnualSalary = 150_000 },
        };
        var s = SearchStore.FromPreferences(user);

        Assert.Multiple(() =>
        {
            Assert.That(s.UserProfileId, Is.EqualTo(7));
            Assert.That(s.SearchTerms, Is.EqualTo(new[] { ".NET Developer", "Full Stack Engineer" }));
            Assert.That(s.Filters.Workplaces, Is.EqualTo(new[] { WorkplaceType.Remote, WorkplaceType.Hybrid }));
            Assert.That(s.Filters.MinAnnualSalary, Is.EqualTo(150_000));
            Assert.That(s.Filters.EmploymentTypes, Does.Contain(EmploymentType.FullTime).And.Not.Contain(EmploymentType.PartTime));
            Assert.That(s.Filters.CompanyBlocklist, Is.EqualTo(new[] { "Initech" }));
        });
    }

    [Test]
    public async Task ASearch_CanNeverBeMovedToAnotherApplicant()
    {
        using var db = new TestDb();
        var profiles = new ProfileStore(db, TimeProvider.System, NullLogger<ProfileStore>.Instance);
        var bob = await profiles.SaveAsync(ProfileStoreTests.Sample("Bob", "Kay"));
        var alice = await profiles.SaveAsync(ProfileStoreTests.Sample("Alice", "Ng"));
        var store = new SearchStore(db, NullLogger<SearchStore>.Instance);
        var bobs = await store.GetOrCreatePrimaryAsync(bob);

        bobs.UserProfileId = alice.Id;                       // a stale form, saved as the wrong applicant
        Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(bobs));
        Assert.That((await store.ListAsync(bob.Id)).Single().Id, Is.EqualTo(bobs.Id), "Bob still has his requirements");
    }

    [Test]
    public async Task GetOrCreatePrimary_CreatesOnce_ThenReturnsTheSameSearch()
    {
        using var db = new TestDb();
        var user = await new ProfileStore(db, TimeProvider.System, NullLogger<ProfileStore>.Instance).SaveAsync(ProfileStoreTests.Sample());
        var store = new SearchStore(db, NullLogger<SearchStore>.Instance);

        var first = await store.GetOrCreatePrimaryAsync(user);
        first.SearchTerms.Add("C# Engineer");
        await store.SaveAsync(first);
        var again = await store.GetOrCreatePrimaryAsync(user);

        Assert.That(again.Id, Is.EqualTo(first.Id));
        Assert.That(again.SearchTerms, Does.Contain("C# Engineer"));
    }
}
