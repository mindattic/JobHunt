using System.Text;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;

namespace JobHunt.Core.Documents;

/// <summary>
/// File and folder names for the tailored documents. Recruiters see the file name, so it carries
/// the applicant's name — "Ryan-DeBraal-Résumé.docx", "Ryan-DeBraal-Cover-Letter.docx" — with the
/// accented é kept intact (NTFS, Word and every upload form handle it).
/// </summary>
public static class DocumentNaming
{
    public const string ResumeSuffix = "Résumé.docx";
    public const string CoverLetterSuffix = "Cover-Letter.docx";

    public static string ResumeFileName(UserProfile profile) => Prefixed(profile, ResumeSuffix);

    public static string CoverLetterFileName(UserProfile profile) => Prefixed(profile, CoverLetterSuffix);

    /// <summary>
    /// "Acme Corp - Senior .NET Developer (linkedin-4012345678)" — readable in Explorer, unique
    /// per posting (the board id + external id suffix), and safe on Windows.
    /// </summary>
    public static string JobFolderName(JobPosting job)
    {
        var readable = SafeSegment($"{job.Company} - {job.Title}", maxLength: 90);
        return $"{readable} ({SafeSegment(job.BoardId, 20)}-{SafeSegment(job.ExternalId, 40)})";
    }

    private static string Prefixed(UserProfile profile, string suffix)
    {
        var name = string.Join('-', new[] { profile.Contact.FirstName, profile.Contact.LastName }
            .Select(n => NamePart(n))
            .Where(n => n.Length > 0));
        return name.Length == 0 ? suffix : $"{name}-{suffix}";
    }

    /// <summary>Spaces become hyphens ("Mary Ann" → "Mary-Ann"); letters, including accented
    /// ones, are kept; characters Windows forbids are dropped.</summary>
    private static string NamePart(string? part)
    {
        var sb = new StringBuilder();
        foreach (var c in (part ?? "").Trim())
        {
            if (char.IsLetterOrDigit(c) || c is '\'' or '.') sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-') { if (sb.Length > 0 && sb[^1] != '-') sb.Append('-'); }
        }
        return sb.ToString().Trim('-', '.');
    }

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    internal static string SafeSegment(string? text, int maxLength)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((text ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength].TrimEnd(' ', '.');
        if (cleaned.Length == 0) cleaned = "_";
        return ReservedNames.Contains(cleaned) ? "_" + cleaned : cleaned;
    }
}
