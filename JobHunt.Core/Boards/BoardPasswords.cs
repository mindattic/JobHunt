using System.Security.Cryptography;
using System.Text;
using MindAttic.Vault.Credentials;

namespace JobHunt.Core.Boards;

/// <summary>
/// Job-site passwords, one per user per site. Kept in MindAttic.Vault (HOUSE-LAW-3) under %APPDATA%\MindAttic\JobHunt —
/// never in the JobHunt database, an export, or a log — and encrypted with Windows DPAPI for the
/// current Windows user before Vault writes them, because Vault's files are plain JSON. A copied
/// file is useless on another machine or to another Windows account.
/// </summary>
public sealed class BoardPasswords
{
    private const string Prefix = "board-";
    private const string Marker = "dpapi:";
    private readonly CredentialStore store;

    public BoardPasswords(string? directory = null)
    {
        directory ??= Environment.GetEnvironmentVariable("JOBHUNT_SECRETS_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "JobHunt");
        store = new CredentialStore(directory);
    }

    public void Set(int userId, string boardId, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("A password is required.", nameof(password));
        var sealedBytes = Protect(Encoding.UTF8.GetBytes(password), Scope(userId, boardId));
        store.SetKey(Key(userId, boardId), Marker + Convert.ToBase64String(sealedBytes));
    }

    /// <summary>The saved password, or null when none is saved — or when it was saved by a
    /// different Windows user or machine, which DPAPI cannot open.</summary>
    public string? Get(int userId, string boardId)
    {
        var stored = store.GetKey(Key(userId, boardId));
        if (stored is null || !stored.StartsWith(Marker, StringComparison.Ordinal)) return null;
        try
        {
            return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(stored[Marker.Length..]), Scope(userId, boardId)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public bool Has(int userId, string boardId) => Get(userId, boardId) is not null;

    public void Clear(int userId, string boardId) => store.SetKeys(Key(userId, boardId), []);

    private static string Key(int userId, string boardId) => $"{Prefix}{userId}-{boardId}";

    private static string Scope(int userId, string boardId) => $"{userId}:{boardId}";

    /// <summary>Per-user, per-board entropy, so one blob can't be replayed as another's.</summary>
    private static byte[] Entropy(string scope) => Encoding.UTF8.GetBytes("MindAttic.JobHunt.Board:" + scope);

    private static byte[] Protect(byte[] data, string scope) => OperatingSystem.IsWindows()
        ? ProtectedData.Protect(data, Entropy(scope), DataProtectionScope.CurrentUser)
        : throw new PlatformNotSupportedException("Saving job-site passwords needs Windows (DPAPI).");

    private static byte[] Unprotect(byte[] data, string scope) => OperatingSystem.IsWindows()
        ? ProtectedData.Unprotect(data, Entropy(scope), DataProtectionScope.CurrentUser)
        : throw new PlatformNotSupportedException("Reading job-site passwords needs Windows (DPAPI).");
}
