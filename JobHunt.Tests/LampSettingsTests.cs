using JobHunt.Core.Llm;
using JobHunt.Core.Settings;
using MindAttic.Vault.Paths;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;

namespace JobHunt.Tests;

/// <summary>Every test in this assembly sees temp Vault roots, so nothing can write the real AppData.</summary>
[SetUpFixture]
public sealed class TestVaultRoots
{
    private string root = "";

    [OneTimeSetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "JobHuntVault-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, Path.Combine(root, "roaming"));
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, Path.Combine(root, "local"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, null);
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, null);
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

/// <summary>LAMP (JH-T22): JobHunt's preferences in Vault, imported from the database row, one test per setting.</summary>
[NonParallelizable]
public class LampSettingsTests
{
    private string dir = "";
    private TestDb db = null!;
    private JsonSettingsStore<JobHuntSettings> Vault() => new(dir);
    private SettingsStore Store() => new(db, Vault());

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "jobhunt-lamp-" + Guid.NewGuid().ToString("N"));
        db = new TestDb();
    }

    [TearDown]
    public void TearDown()
    {
        db.Dispose();
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    /// <summary>Today's preferences: the single database row the pre-LAMP app wrote.</summary>
    private void TodaysRow(Action<AppSettings> set)
    {
        using var ctx = db.CreateDbContext();
        var row = new AppSettings();
        set(row);
        ctx.Settings.Add(row);
        ctx.SaveChanges();
    }

    private AppSettings EditInVault(Action<JobHuntSettings> edit)
    {
        Vault().Update(edit);
        return Store().Current;
    }

    [Test]
    public void Setting_SelectedLlmProvider_ImportsRoundTripsAndIsTheProviderLegionTriesFirst()
    {
        TodaysRow(r => r.SelectedLlmProvider = LlmProviders.Gemini);
        Assert.That(Store().Current.SelectedLlmProvider, Is.EqualTo(LlmProviders.Gemini), "imported from the database");
        var s = EditInVault(v => v.SelectedLlmProvider = LlmProviders.Kimi);
        Assert.That(LlmProviders.FallbackChain(s.SelectedLlmProvider)[0], Is.EqualTo(LlmProviders.Kimi));
    }

    [Test]
    public void Setting_DryRun_ImportsRoundTripsAndDecidesWhetherApplicationsAreReal()
    {
        TodaysRow(r => r.DryRun = false);
        Assert.That(Store().Current.DryRun, Is.False);
        Assert.That(EditInVault(v => v.DryRun = true).DryRun, Is.True, "Apply and the badge read Current.DryRun");
    }

    [Test]
    public void Setting_DailyApplyCap_ImportsRoundTripsAndIsClamped()
    {
        TodaysRow(r => r.DailyApplyCap = 40);
        Assert.That(Store().Current.DailyApplyCap, Is.EqualTo(40));
        Assert.That(EditInVault(v => v.DailyApplyCap = 12).DailyApplyCap, Is.EqualTo(12));
        Assert.That(EditInVault(v => v.DailyApplyCap = 999).DailyApplyCap, Is.EqualTo(200), "same 1..200 bound the old Settings tab enforced");
    }

    [Test]
    public void Setting_PreflightQuestionScan_ImportsAndRoundTrips()
    {
        TodaysRow(r => r.PreflightQuestionScan = false);
        Assert.That(Store().Current.PreflightQuestionScan, Is.False);
        Assert.That(EditInVault(v => v.PreflightQuestionScan = true).PreflightQuestionScan, Is.True);
    }

    [Test]
    public void Setting_MinActionDelayMs_ImportsRoundTripsAndPacesTheBrowser()
    {
        TodaysRow(r => { r.MinActionDelayMs = 1000; r.MaxActionDelayMs = 1000; });
        Assert.That(Store().Current.MinActionDelayMs, Is.EqualTo(1000));
        var s = EditInVault(v => { v.MinActionDelayMs = 5; v.MaxActionDelayMs = 5; });
        Assert.That((s.MinActionDelayMs, s.MaxActionDelayMs), Is.EqualTo((5, 5)), "RandomPacer reads this range");
    }

    [Test]
    public void Setting_MaxActionDelayMs_ImportsRoundTripsAndNeverFallsBelowTheMinimum()
    {
        TodaysRow(r => r.MaxActionDelayMs = 4000);
        Assert.That(Store().Current.MaxActionDelayMs, Is.EqualTo(4000));
        var s = EditInVault(v => { v.MinActionDelayMs = 900; v.MaxActionDelayMs = 100; });
        Assert.That(s.MaxActionDelayMs, Is.EqualTo(900));
    }

    [Test]
    public void Setting_Theme_ImportsAndRoundTrips()
    {
        TodaysRow(r => r.Theme = "light");
        Assert.That(Store().Current.Theme, Is.EqualTo("light"));
        Assert.That(EditInVault(v => v.Theme = "dark").Theme, Is.EqualTo("dark"));
    }

    [Test]
    public void EverySettingHasItsOwnTest()
    {
        var tested = GetType().GetMethods().Select(m => m.Name).Where(n => n.StartsWith("Setting_")).ToList();
        foreach (var field in SettingsManifestBuilder.Build<JobHuntSettings>().Fields)
            Assert.That(tested.Any(t => t.StartsWith("Setting_" + char.ToUpperInvariant(field.Property[0]) + field.Property[1..] + "_")), Is.True, field.Property);
    }

    [Test]
    public void TheActiveApplicantStaysInTheDatabase()
    {
        var store = Store();
        var s = store.Current;
        s.ActiveProfileId = 7;
        store.Save(s);
        Assert.That(Store().Current.ActiveProfileId, Is.EqualTo(7));
        Assert.That(File.ReadAllText(Vault().FilePath), Does.Not.Contain("activeProfileId"));
        Assert.That(SettingsManifestBuilder.Build<JobHuntSettings>().Fields.Select(f => f.Property), Does.Not.Contain("activeProfileId"));
    }

    [Test]
    public void TheVaultFileWins_OnceItExists()
    {
        TodaysRow(r => r.DailyApplyCap = 40);
        Vault().Save(new JobHuntSettings { DailyApplyCap = 9 });
        Assert.That(Store().Current.DailyApplyCap, Is.EqualTo(9));
    }

    [Test]
    public async Task VaultSaves_RefreshTheRunningApp()
    {
        using var store = Store();
        _ = store.Current;
        var seen = new TaskCompletionSource<AppSettings>();
        store.Changed += s => seen.TrySetResult(s);
        store.Watch();
        Vault().Update(v => v.DryRun = false);
        var s = await seen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(s.DryRun, Is.False);
        Assert.That(store.Current.DryRun, Is.False);
    }

    [Test]
    public void ManifestDeclaresTheLlmKeysAndNoSecrets()
    {
        var m = SettingsManifestBuilder.Build<JobHuntSettings>();
        Assert.That(m.Credentials.Select(c => c.ProviderId), Is.EquivalentTo(LlmProviders.All.Select(p => p.Id)));
        Assert.That(m.Fields.All(f => !string.IsNullOrWhiteSpace(f.Label)), Is.True);
    }

    [Test]
    public void TheOldSettingsCardsAreGone()
    {
        var html = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "JobHunt.App", "wwwroot", "panel.html"));
        Assert.That(html, Does.Not.Contain("id=\"dry-run\"").And.Not.Contain("id=\"provider-keys\"").And.Contain("id=\"open-vault\""));
    }
}
