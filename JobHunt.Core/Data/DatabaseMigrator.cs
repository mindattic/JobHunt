using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Data;

/// <summary>What happened when the database was brought up to date at startup.</summary>
/// <param name="SetAsidePath">When not null, the old database file was not one this build could
/// upgrade; it was moved here (never deleted) and a fresh database was created.</param>
public sealed record MigrationOutcome(string? SetAsidePath);

/// <summary>
/// Creates the database or upgrades it to this build's schema — and never lets an unrecognizable
/// database crash startup.
/// <para>
/// A database whose recorded migrations include one this build doesn't have was made by a build
/// with a different migration history (a pre-release that was reset). EF can't upgrade that: it
/// would try to create tables that already exist. Such a file is moved aside intact — database and
/// WAL files together, renamed with a timestamp — and a fresh database is created, so nothing is
/// lost and the app starts. Ordinary upgrades (every applied migration known) just migrate.
/// </para>
/// </summary>
public sealed class DatabaseMigrator(IDbContextFactory<JobHuntDb> dbFactory, JobHuntPaths paths, TimeProvider clock, ILogger<DatabaseMigrator> log)
{
    public MigrationOutcome Migrate()
    {
        string[] unknown;
        using (var db = dbFactory.CreateDbContext())
        {
            var known = db.Database.GetMigrations().ToHashSet();
            unknown = File.Exists(paths.DatabasePath)
                ? db.Database.GetAppliedMigrations().Where(m => !known.Contains(m)).ToArray()
                : [];
        }

        string? setAside = null;
        if (unknown.Length > 0)
        {
            setAside = SetAside();
            log.LogWarning("The database was made by a build with migrations this one doesn't know ({Unknown}); it was moved to {Path} and a fresh one created",
                string.Join(", ", unknown), setAside);
        }

        using (var db = dbFactory.CreateDbContext())
            db.Database.Migrate();
        return new MigrationOutcome(setAside);
    }

    /// <summary>Moves jobhunt.db and its -wal/-shm companions to a timestamped name beside it.</summary>
    private string SetAside()
    {
        SqliteConnection.ClearAllPools();   // release every handle before renaming the files
        var stamp = clock.GetLocalNow().ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(Path.GetDirectoryName(paths.DatabasePath)!, $"jobhunt.unrecognized-{stamp}.db");
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var from = paths.DatabasePath + suffix;
            if (File.Exists(from)) File.Move(from, target + suffix);
        }
        return target;
    }
}
