using Microsoft.Extensions.Logging;
using MindAttic.Legion;

namespace JobHunt.Core.Llm;

public sealed record LlmReply(string ProviderId, string Text);

/// <summary>The one way JobHunt calls a language model — summarizing postings, extracting job
/// facts, tailoring documents, proposing screening answers.</summary>
public interface IJobHuntLlm
{
    Task<LlmReply> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct);
}

/// <summary>
/// Routes through MindAttic.Legion (HOUSE-LAW-4): the user's selected provider first, the others as
/// fallbacks, each provider's key coming from <see cref="ByokKeys"/>. Legion owns retry and
/// circuit-breaking.
/// </summary>
public sealed class LegionJobHuntLlm(LegionClient legion, Func<string?> selectedProvider, ILogger<LegionJobHuntLlm> log)
    : IJobHuntLlm
{
    public async Task<LlmReply> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct)
    {
        var chain = LlmProviders.FallbackChain(selectedProvider());
        log.LogDebug("LLM call via {Chain}, {Chars} chars in", string.Join(" → ", chain), userMessage.Length);
        var (provider, text) = await legion.CallWithFallbackAsync(chain, systemPrompt, userMessage, maxTokens, temperature: 0.4, ct);
        log.LogInformation("LLM answered via {Provider}, {Chars} chars out", provider, text.Length);
        return new LlmReply(provider, text);
    }
}
