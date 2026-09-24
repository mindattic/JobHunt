namespace JobHunt.Core.Llm;

/// <summary>The providers a user can bring their own key for — the same four Automata offers.
/// Ids are MindAttic.Legion's provider ids.</summary>
public static class LlmProviders
{
    public const string Claude = "claude";
    public const string OpenAi = "openai";
    public const string Gemini = "gemini";
    public const string Kimi = "kimi";

    public static readonly IReadOnlyList<(string Id, string DisplayName)> All =
    [
        (Claude, "Claude (Anthropic)"),
        (OpenAi, "ChatGPT (OpenAI)"),
        (Gemini, "Gemini (Google)"),
        (Kimi, "Kimi (Moonshot)"),
    ];

    public static bool IsKnown(string? id) => All.Any(p => p.Id == id);

    /// <summary><paramref name="selected"/> first, the rest behind it in catalog order.</summary>
    public static IReadOnlyList<string> FallbackChain(string? selected)
    {
        var chain = new List<string>();
        if (IsKnown(selected)) chain.Add(selected!);
        chain.AddRange(All.Select(p => p.Id).Where(id => id != selected));
        return chain;
    }
}
