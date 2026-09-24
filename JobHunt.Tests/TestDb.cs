using JobHunt.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Tests;

/// <summary>A real SQLite database in memory, brought up by the app's own migrations — so tests
/// exercise the schema users actually get, not a model-only approximation.</summary>
internal sealed class TestDb : IDbContextFactory<JobHuntDb>, IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly DbContextOptions<JobHuntDb> options;

    public TestDb()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<JobHuntDb>().UseSqlite(connection).Options;
        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public JobHuntDb CreateDbContext() => new(options);

    public void Dispose() => connection.Dispose();
}

/// <summary>A clock tests can set.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
