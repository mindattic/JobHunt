using JobHunt.Core.Data;
using Microsoft.EntityFrameworkCore;
using MindAttic.Vault.Settings;

namespace JobHunt.Core.Settings;

/// <summary>
/// The app's settings: preferences from MindAttic Vault (<see cref="JobHuntSettings"/>, edited in MindAttic.Vault.Desktop)
/// overlaid on the single database row, which still holds which applicant is active. The first run under Vault
/// imports the row's preferences. <see cref="Current"/> is cached so hot paths (the LLM provider choice, read on
/// every call) never hit the disk; <see cref="Watch"/> refreshes it when Vault saves.
/// </summary>
public sealed class SettingsStore(IDbContextFactory<JobHuntDb> dbFactory, JsonSettingsStore<JobHuntSettings>? vault = null) : IDisposable
{
    private readonly JsonSettingsStore<JobHuntSettings> vault = vault ?? JsonSettingsStore<JobHuntSettings>.ForSettingsApp();
    private AppSettings? cached;
    private FileSystemWatcher? watcher;
    private Timer? debounce;

    public AppSettings Current => cached ??= Load();

    /// <summary>Raised (on a worker thread) after Vault saved new preferences and <see cref="Current"/> was refreshed.</summary>
    public event Action<AppSettings>? Changed;

    public string VaultFilePath => vault.FilePath;

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
        if (!vault.Exists()) vault.Save(JobHuntSettings.From(row));
        vault.LoadOrSeed().ApplyTo(row);
        return cached = row;
    }

    public void Save(AppSettings settings)
    {
        using (var db = dbFactory.CreateDbContext())
        {
            if (settings.Id == 0) db.Settings.Add(settings);
            else db.Settings.Update(settings);
            db.SaveChanges();
        }
        vault.Save(JobHuntSettings.From(settings));
        cached = settings;
    }

    /// <summary>Starts refreshing <see cref="Current"/> when MindAttic.Vault.Desktop saves JobHunt's settings.</summary>
    public void Watch()
    {
        if (watcher is not null) return;
        Directory.CreateDirectory(vault.Directory);
        watcher = new FileSystemWatcher(vault.Directory, vault.FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        FileSystemEventHandler changed = (_, _) => (debounce ??= new Timer(_ => Refresh())).Change(300, Timeout.Infinite);
        watcher.Changed += changed;
        watcher.Created += changed;
        watcher.Renamed += (s, e) => changed(s, e);
        watcher.EnableRaisingEvents = true;
    }

    private void Refresh()
    {
        var row = Current;
        vault.Load().ApplyTo(row);
        Changed?.Invoke(row);
    }

    public void Dispose()
    {
        watcher?.Dispose();
        debounce?.Dispose();
    }
}
