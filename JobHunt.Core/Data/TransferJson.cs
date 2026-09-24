using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JobHunt.Core.Data;

/// <summary>
/// The serializer for every JobHunt export file (profile, full backup). Database ids and foreign
/// keys are left out, so an imported file always becomes fresh rows and never collides with what
/// is already in the target database; computed get-only properties (FullName, FactRef…) are left
/// out because they are derived.
/// </summary>
internal static class TransferJson
{
    private static readonly HashSet<string> Dropped =
    [
        "id", "normalizedQuestion", "jobPosting", "jobPostingId", "jobApplicationId", "searchProfileId",
        "userProfileId", "activeProfileId",
    ];

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Human-readable file: accented names stay as written instead of \u-escapes. Safe — the
        // output is a file, never HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { StripIdsAndComputed } },
    };

    private static void StripIdsAndComputed(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.Name.EndsWith("Envelope", StringComparison.Ordinal)) return;
        for (var i = info.Properties.Count - 1; i >= 0; i--)
        {
            var p = info.Properties[i];
            if (Dropped.Contains(p.Name) || p.Set is null) info.Properties.RemoveAt(i);
        }
    }
}
