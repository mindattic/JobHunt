using JobHunt.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Core.Settings;

/// <summary>The single <see cref="AppSettings"/> row. <see cref="Current"/> is cached so hot paths
/// (the LLM provider choice, read on every call) never hit the database.</summary>
public sealed class SettingsStore(IDbContextFactory<JobHuntDb> dbFactory)
{
    private AppSettings? cached;

    public AppSettings Current => cached ??= Load();

    public AppSettings Load()
    {
        using var db = dbFactory.CreateDbContext();
        var row = db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefault();
        if (row is null)
        {
            row = new AppSettings();
            db.Settings.Add(row);
            db.SaveChanges();
        }
        return cached = row;
    }

    public void Save(AppSettings settings)
    {
        using var db = dbFactory.CreateDbContext();
        if (settings.Id == 0) db.Settings.Add(settings);
        else db.Settings.Update(settings);
        db.SaveChanges();
        cached = settings;
    }
}
