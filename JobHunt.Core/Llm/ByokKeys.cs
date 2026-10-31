using JobHunt.Core.Settings;
using MindAttic.Vault.Credentials;

namespace JobHunt.Core.Llm;

/// <summary>
/// JobHunt's LLM keys: the shared MindAttic named keys (edited on MindAttic.Vault.Desktop's Keys tab), in the order
/// JobHunt's assignment on its Vault tab gives (or the shared order), skipping disabled and failed keys. Keys JobHunt
/// stored for itself before LAMP ("jobhunt-&lt;provider&gt;") are moved into the shared list on first start and stay
/// JobHunt's active keys. Read live on every call, so a change in Vault takes effect without a restart.
/// </summary>
public sealed class ByokKeys
{
    private readonly KeySelector selector;

    public ByokKeys() : this(KeySelector.ForCurrentUser())
    {
        KeyImporter.ImportAppScopedFor(JobHuntSettings.AppId);
    }

    public ByokKeys(KeySelector selector) => this.selector = selector;

    /// <summary>True when JobHunt has a usable key for the provider.</summary>
    public bool HasKey(string providerId) => selector.Plan(providerId, JobHuntSettings.AppId).Usable.Count > 0;

    /// <summary>The key Legion should use for the provider, or null when none is usable.</summary>
    public string? Resolve(string providerId) => selector.GetActiveKey(providerId, JobHuntSettings.AppId)?.Value;
}
