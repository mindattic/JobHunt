namespace JobHunt.Core.Data;

/// <summary>
/// Where JobHunt keeps things on this machine.
/// <list type="bullet">
/// <item><see cref="DataRoot"/> — %LocalAppData%\MindAttic\JobHunt: the database, logs and the
/// board sign-in browser profile. Machine-local; never synced.</item>
/// <item><see cref="DocumentsRoot"/> — Documents\JobHunt\Applications: one folder per job holding
/// its tailored résumé and cover letter, where the user can open and edit them in Word.</item>
/// </list>
/// JOBHUNT_DATA_DIR / JOBHUNT_DOCUMENTS_DIR override both, for tests and a portable install.
/// </summary>
public sealed class JobHuntPaths
{
    public JobHuntPaths(string? dataRoot = null, string? documentsRoot = null)
    {
        DataRoot = dataRoot
            ?? Environment.GetEnvironmentVariable("JOBHUNT_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "JobHunt");
        DocumentsRoot = documentsRoot
            ?? Environment.GetEnvironmentVariable("JOBHUNT_DOCUMENTS_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JobHunt", "Applications");
    }

    public string DataRoot { get; }
    public string DocumentsRoot { get; }
    public string DatabasePath => Path.Combine(DataRoot, "jobhunt.db");
    public string LogsDirectory => Path.Combine(DataRoot, "Logs");
    /// <summary>Each user gets their own browser profile, so a job-site session never crosses users.</summary>
    public string BrowserProfileDirectory(int userId) => Path.Combine(DataRoot, "WebView2", $"user-{userId}");
    public string PanelProfileDirectory => Path.Combine(DataRoot, "PanelWebView2");

    public string ConnectionString => $"Data Source={DatabasePath}";

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DocumentsRoot);
    }
}
