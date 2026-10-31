using JobHunt.Core.Llm;
using MindAttic.Vault.Settings.Manifest;

namespace JobHunt.Core.Settings;

/// <summary>
/// JobHunt's app-wide preferences in MindAttic Vault (<c>%APPDATA%\MindAttic\JobHunt\settings.json</c>), edited in
/// MindAttic.Vault.Desktop. Which applicant is active stays in the database; API keys are the shared named keys
/// (Keys tab), assigned to JobHunt on its tab.
/// </summary>
[SettingsApp(AppId, "JobHunt", Icon = "briefcase", Description = "Finds jobs that fit each applicant and applies for them.")]
[UsesCredential(LlmProviders.Claude, Purpose = "Summarizes postings and tailors documents (first choice by default)")]
[UsesCredential(LlmProviders.OpenAi, Purpose = "Fallback LLM")]
[UsesCredential(LlmProviders.Gemini, Purpose = "Fallback LLM")]
[UsesCredential(LlmProviders.Kimi, Purpose = "Fallback LLM")]
public sealed class JobHuntSettings
{
    public const string AppId = "JobHunt";

    [Setting("Use first", Group = "AI provider", Order = 1, Help = "The LLM JobHunt tries first; the others are fallbacks.")]
    [SettingChoices(LlmProviders.Claude, LlmProviders.OpenAi, LlmProviders.Gemini, LlmProviders.Kimi)]
    public string SelectedLlmProvider { get; set; } = LlmProviders.Claude;

    [Setting("Dry run", Group = "Applying", Order = 1,
        Help = "Recommended until you trust it: does everything a real application does and stops before the final Submit. Off is the only way anything reaches an employer.")]
    public bool DryRun { get; set; } = true;

    [Setting("Real applications per day", Group = "Applying", Order = 2)]
    [SettingRange(1, 200)]
    public int DailyApplyCap { get; set; } = 25;

    [Setting("Collect screening questions while hunting", Group = "Applying", Order = 3,
        Help = "Opens each recommended job's application during the hunt to collect its questions (then discards it), so Apply can run unattended.")]
    public bool PreflightQuestionScan { get; set; } = true;

    [Setting("Shortest pause between actions (ms)", Group = "Browser pacing", Order = 1, Help = "Human-paced random pause between browser actions.")]
    [SettingRange(0, 60000)]
    public int MinActionDelayMs { get; set; } = 800;

    [Setting("Longest pause between actions (ms)", Group = "Browser pacing", Order = 2)]
    [SettingRange(0, 60000)]
    public int MaxActionDelayMs { get; set; } = 2500;

    [Setting("Theme", Group = "Appearance", Order = 1)]
    [SettingChoices("dark", "light")]
    public string Theme { get; set; } = "dark";

    public static JobHuntSettings From(AppSettings row) => new()
    {
        SelectedLlmProvider = row.SelectedLlmProvider, DryRun = row.DryRun, DailyApplyCap = row.DailyApplyCap,
        PreflightQuestionScan = row.PreflightQuestionScan, MinActionDelayMs = row.MinActionDelayMs,
        MaxActionDelayMs = row.MaxActionDelayMs, Theme = row.Theme,
    };

    public void ApplyTo(AppSettings row)
    {
        row.SelectedLlmProvider = LlmProviders.IsKnown(SelectedLlmProvider) ? SelectedLlmProvider : LlmProviders.Claude;
        row.DryRun = DryRun;
        row.DailyApplyCap = Math.Clamp(DailyApplyCap, 1, 200);
        row.PreflightQuestionScan = PreflightQuestionScan;
        row.MinActionDelayMs = Math.Max(0, MinActionDelayMs);
        row.MaxActionDelayMs = Math.Max(row.MinActionDelayMs, MaxActionDelayMs);
        row.Theme = Theme;
    }
}
