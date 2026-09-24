using JobHunt.Core.Llm;

namespace JobHunt.Core.Settings;

/// <summary>
/// App-wide preferences — one row. API keys are NOT here: they live in MindAttic.Vault (see
/// <see cref="ByokKeys"/>) so the database can be exported or shared without leaking a secret.
/// </summary>
public sealed class AppSettings
{
    public int Id { get; set; }

    /// <summary>The user the app is working as. Null until the first user is created.</summary>
    public int? ActiveProfileId { get; set; }

    /// <summary>Which LLM runs first; the others are fallbacks. One of <see cref="LlmProviders.All"/>.</summary>
    public string SelectedLlmProvider { get; set; } = LlmProviders.Claude;

    /// <summary>
    /// On by default. Apply runs every application up to — but not including — the final submit
    /// click, and records it as a dry run. Turning this off is the only way anything reaches an
    /// employer.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Real (non-dry-run) submissions allowed per calendar day.</summary>
    public int DailyApplyCap { get; set; } = 25;

    /// <summary>Open each recommended job's application during the hunt to collect its screening
    /// questions (then discard it), so Apply can run unattended.</summary>
    public bool PreflightQuestionScan { get; set; } = true;

    /// <summary>Human-paced random pause between browser actions, in milliseconds.</summary>
    public int MinActionDelayMs { get; set; } = 800;
    public int MaxActionDelayMs { get; set; } = 2500;

    public string Theme { get; set; } = "dark";
}
