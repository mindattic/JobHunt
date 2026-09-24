using MindAttic.Vault.Credentials;

namespace JobHunt.Core.Llm;

/// <summary>
/// Bring-your-own-key storage, the same scheme Automata uses. A key entered in JobHunt's Settings is
/// JobHunt's own — stored in MindAttic.Vault under the app-scoped id "jobhunt-&lt;provider&gt;" — so it
/// never changes what another MindAttic app resolves. With no key of its own, JobHunt falls back to
/// the shared MindAttic keyring when one is configured on this machine. Both are read live on every
/// call, so a saved key takes effect without a restart.
/// </summary>
public sealed class ByokKeys
{
    public const string AppId = "jobhunt";

    private readonly AppScopedCredentialStore own;
    private readonly ICredentialStore shared;

    public ByokKeys() : this(LlmCredentialStore.Default) { }

    public ByokKeys(ICredentialStore shared)
    {
        this.shared = shared;
        own = new AppScopedCredentialStore(AppId, shared);
    }

    /// <summary>This app's own keys for <paramref name="providerId"/>, in failover order.</summary>
    public IReadOnlyList<string> GetOwnKeys(string providerId) =>
        own.GetKeys(providerId).Select(k => k.Key).ToList();

    /// <summary>Adds keys to this app's own pool for a provider (duplicates ignored). Returns how
    /// many were new.</summary>
    public int AddOwnKeys(string providerId, IEnumerable<string> keys)
    {
        var existing = GetOwnKeys(providerId);
        var added = keys.Select(k => k.Trim()).Where(k => k.Length > 0 && !existing.Contains(k)).Distinct().ToList();
        if (added.Count > 0) SetOwnKeys(providerId, existing.Concat(added));
        return added.Count;
    }

    /// <summary>Replaces this app's own key pool for a provider; an empty list clears it.</summary>
    public void SetOwnKeys(string providerId, IEnumerable<string> keys) =>
        own.SetKeys(providerId, keys
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct()
            .Select(k => new CredentialPoolEntry(k))
            .ToList());

    public bool HasOwnKey(string providerId) => GetOwnKeys(providerId).Count > 0;

    /// <summary>True only when a shared key really exists on this machine — the UI says "Not
    /// configured" rather than implying a fallback that isn't there.</summary>
    public bool HasSharedKey(string providerId)
    {
        try { return !string.IsNullOrWhiteSpace(shared.GetKey(providerId)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The key Legion should use: this app's own first, else null so Legion falls back to
    /// the shared store itself.</summary>
    public string? Resolve(string providerId) => GetOwnKeys(providerId).FirstOrDefault();
}
