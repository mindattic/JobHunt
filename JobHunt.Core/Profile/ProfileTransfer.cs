using System.Text.Json;
using JobHunt.Core.Data;

namespace JobHunt.Core.Profile;

/// <summary>
/// Export and import of the applicant profile alone as one portable JSON file
/// (<c>*.jobhunt-profile.json</c>) — a backup, a move to a new machine, or a starting point shared
/// with someone else. <see cref="DatabaseTransfer"/> exports everything else too.
/// </summary>
public static class ProfileTransfer
{
    public const string Format = "jobhunt-profile";
    public const int CurrentVersion = 1;
    public const string FileExtension = ".jobhunt-profile.json";

    public static string Export(UserProfile profile, DateTimeOffset exportedAt) =>
        JsonSerializer.Serialize(new ProfileEnvelope
        {
            Format = Format,
            Version = CurrentVersion,
            ExportedAt = exportedAt,
            Profile = profile,
        }, TransferJson.Options);

    /// <summary>Parses an exported file. Throws <see cref="FormatException"/> with a readable
    /// message for anything that isn't one.</summary>
    public static UserProfile Import(string json)
    {
        ProfileEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<ProfileEnvelope>(json, TransferJson.Options); }
        catch (JsonException ex) { throw new FormatException($"Not a JobHunt profile file: {ex.Message}", ex); }

        if (envelope is null || envelope.Format != Format)
            throw new FormatException("Not a JobHunt profile file (missing \"format\": \"jobhunt-profile\").");
        if (envelope.Version > CurrentVersion)
            throw new FormatException($"This profile was exported by a newer JobHunt (format version {envelope.Version}). Update the app to import it.");

        var profile = envelope.Profile ?? new UserProfile();
        foreach (var answer in profile.Answers) answer.NormalizedQuestion = ScreeningAnswer.Normalize(answer.Question);
        return profile;
    }

    private sealed class ProfileEnvelope
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public DateTimeOffset ExportedAt { get; set; }
        public UserProfile? Profile { get; set; }
    }
}
