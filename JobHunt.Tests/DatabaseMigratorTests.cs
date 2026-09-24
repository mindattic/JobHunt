using JobHunt.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

/// <summary>Startup migration against real database FILES (not in-memory), the way the app runs.</summary>
[TestFixture]
public class DatabaseMigratorTests
{
    private string dir = null!;
    private JobHuntPaths paths = null!;
    private DbFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Directory.CreateTempSubdirectory("jobhunt-migrate-").FullName;
        paths = new JobHuntPaths(dir, Path.Combine(dir, "docs"));
        factory = new DbFactory(paths.ConnectionString);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(dir, recursive: true);
    }

    private MigrationOutcome Migrate() =>
        new DatabaseMigrator(factory, paths, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).Migrate();

    [Test]
    public void AFreshInstall_CreatesTheDatabase()
    {
        var outcome = Migrate();
        Assert.That(outcome.SetAsidePath, Is.Null);
        using var db = factory.CreateDbContext();
        Assert.That(db.Database.GetPendingMigrations(), Is.Empty);
    }

    [Test]
    public void AnUpToDateDatabase_IsLeftAlone_AndKeepsItsData()
    {
        Migrate();
        using (var db = factory.CreateDbContext()) { db.Settings.Add(new() { DailyApplyCap = 7 }); db.SaveChanges(); }

        var outcome = Migrate();
        using var again = factory.CreateDbContext();
        Assert.That(outcome.SetAsidePath, Is.Null);
        Assert.That(again.Settings.Single().DailyApplyCap, Is.EqualTo(7));
    }

    [Test]
    public void ADatabaseFromAnUnknownMigrationHistory_IsSetAside_NotCrashedOn()
    {
        // What a reset pre-release build left behind: tables exist, but under a migration id this
        // build doesn't have — exactly the "table already exists" crash from the field.
        Migrate();
        using (var db = factory.CreateDbContext())
        {
            db.Database.ExecuteSqlRaw("UPDATE \"__EFMigrationsHistory\" SET \"MigrationId\" = '20260923225514_Initial'");
            db.Settings.Add(new() { DailyApplyCap = 3 });
            db.SaveChanges();
        }

        var outcome = Migrate();

        Assert.That(outcome.SetAsidePath, Is.Not.Null);
        Assert.That(File.Exists(outcome.SetAsidePath), Is.True, "the old file is kept, not deleted");
        using var fresh = factory.CreateDbContext();
        Assert.That(fresh.Database.GetPendingMigrations(), Is.Empty);
        Assert.That(fresh.Settings.Count(), Is.Zero, "a fresh database was started");

        SqliteConnection.ClearAllPools();
        using var old = new SqliteConnection($"Data Source={outcome.SetAsidePath}");
        old.Open();
        using var cmd = old.CreateCommand();
        cmd.CommandText = "SELECT DailyApplyCap FROM Settings";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(3), "and the set-aside copy still has its data");
    }

    private sealed class DbFactory(string connection) : IDbContextFactory<JobHuntDb>
    {
        public JobHuntDb CreateDbContext() => new(new DbContextOptionsBuilder<JobHuntDb>().UseSqlite(connection).Options);
    }
}
