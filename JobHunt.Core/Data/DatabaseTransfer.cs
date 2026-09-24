using System.Text.Json;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using JobHunt.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Data;

/// <summary>One user and everything that belongs to them.</summary>
public sealed class UserBackup
{
    public UserProfile Profile { get; set; } = new();
    public List<SearchProfile> Searches { get; set; } = [];
    /// <summary>Each job carries its applications and their outcome history.</summary>
    public List<JobPosting> Jobs { get; set; } = [];
}

/// <summary>Everything a JobHunt database holds, as one exportable document.</summary>
public sealed class DatabaseBackup
{
    public List<UserBackup> Users { get; set; } = [];
    public AppSettings? Settings { get; set; }
}

/// <summary>
/// Whole-database export and import as one JSON file (<c>*.jobhunt-backup.json</c>): every user
/// (deleted ones included, still marked deleted) with their profile, job requirements, jobs, and
/// applications with outcome history; plus app settings. Import replaces what the database holds,
/// in one transaction. Secrets are never included — API keys and job-site passwords live in
/// MindAttic.Vault, not the database. Hunt-run history is not exported.
/// </summary>
public sealed class DatabaseTransfer(IDbContextFactory<JobHuntDb> dbFactory, TimeProvider clock, ILogger<DatabaseTransfer> log)
{
    public const string Format = "jobhunt-backup";
    public const int CurrentVersion = 1;
    public const string FileExtension = ".jobhunt-backup.json";

    public async Task<string> ExportAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var profiles = await ProfileStore.WithEverything(db.Profiles).AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
        var searches = await db.Searches.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);
        var jobs = await db.Jobs.AsNoTracking()
            .Include(j => j.Applications).ThenInclude(a => a.Events)
            .AsSplitQuery().OrderBy(j => j.Id).ToListAsync(ct);
        foreach (var app in jobs.SelectMany(j => j.Applications)) app.JobPosting = null!;

        var backup = new DatabaseBackup
        {
            Users = profiles.Select(p => new UserBackup
            {
                Profile = p,
                Searches = searches.Where(s => s.UserProfileId == p.Id).ToList(),
                Jobs = jobs.Where(j => j.UserProfileId == p.Id).ToList(),
            }).ToList(),
            Settings = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct),
        };

        log.LogInformation("Exporting database: {Users} users, {Jobs} jobs, {Applications} applications",
            backup.Users.Count, jobs.Count, jobs.Sum(j => j.Applications.Count));
        return JsonSerializer.Serialize(new BackupEnvelope
        {
            Format = Format, Version = CurrentVersion, ExportedAt = clock.GetUtcNow(), Backup = backup,
        }, TransferJson.Options);
    }

    /// <summary>Parses a backup file without touching the database.</summary>
    public static DatabaseBackup Parse(string json)
    {
        BackupEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<BackupEnvelope>(json, TransferJson.Options); }
        catch (JsonException ex) { throw new FormatException($"Not a JobHunt backup file: {ex.Message}", ex); }
        if (envelope is null || envelope.Format != Format || envelope.Backup is null)
            throw new FormatException("Not a JobHunt backup file (missing \"format\": \"jobhunt-backup\").");
        if (envelope.Version > CurrentVersion)
            throw new FormatException($"This backup was made by a newer JobHunt (format version {envelope.Version}). Update the app to import it.");
        return envelope.Backup;
    }

    /// <summary>Replaces the whole database with the backup's contents. All or nothing. The first
    /// user who isn't deleted becomes the active one.</summary>
    public async Task ImportAsync(string json, CancellationToken ct = default)
    {
        var backup = Parse(json);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.ApplicationEvents.ExecuteDeleteAsync(ct);
        await db.Applications.ExecuteDeleteAsync(ct);
        await db.Jobs.ExecuteDeleteAsync(ct);
        await db.HuntRuns.ExecuteDeleteAsync(ct);
        await db.Searches.ExecuteDeleteAsync(ct);
        await db.Profiles.ExecuteDeleteAsync(ct);
        await db.Settings.ExecuteDeleteAsync(ct);

        // Profiles first: their new ids are what the searches and jobs point at.
        foreach (var user in backup.Users)
        {
            foreach (var a in user.Profile.Answers) a.NormalizedQuestion = ScreeningAnswer.Normalize(a.Question);
            db.Profiles.Add(user.Profile);
        }
        await db.SaveChangesAsync(ct);

        foreach (var user in backup.Users)
        {
            foreach (var s in user.Searches) s.UserProfileId = user.Profile.Id;
            foreach (var j in user.Jobs)
            {
                j.UserProfileId = user.Profile.Id;
                // Profile facts get new ids on import, so "skill:12"-style matches from the old
                // database would point at the wrong fact (possibly another applicant's). Drop them
                // and mark the job for re-analysis rather than trust them.
                foreach (var r in j.Requirements) r.MatchedFactRefs.Clear();
                j.UnderstoodAt = null;
            }
            db.Searches.AddRange(user.Searches);
            db.Jobs.AddRange(user.Jobs);
        }

        var settings = backup.Settings ?? new AppSettings();
        settings.ActiveProfileId = backup.Users.FirstOrDefault(u => u.Profile.DeletedAt is null)?.Profile.Id;
        db.Settings.Add(settings);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Imported database: {Users} users, {Jobs} jobs, {Applications} applications",
            backup.Users.Count, backup.Users.Sum(u => u.Jobs.Count), backup.Users.Sum(u => u.Jobs.Sum(j => j.Applications.Count)));
    }

    private sealed class BackupEnvelope
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public DateTimeOffset ExportedAt { get; set; }
        public DatabaseBackup? Backup { get; set; }
    }
}
