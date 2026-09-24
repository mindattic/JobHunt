using System.Globalization;
using System.Text.RegularExpressions;
using JobHunt.Core.Jobs;

namespace JobHunt.Core.Boards.LinkedIn;

/// <summary>
/// Turns LinkedIn's human wording into data: "Reposted 3 days ago", "Over 100 applicants",
/// "$120K/yr - $150K/yr", "Remote", "Full-time". Pure functions, so every phrasing LinkedIn is
/// known to use is unit-tested.
/// </summary>
public static partial class LinkedInText
{
    /// <summary>"2 weeks ago", "Reposted 3 days ago", "1 hour ago", "30 minutes ago", "Just now".</summary>
    public static DateTimeOffset? ParsePostedAgo(string? text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (JustNow().IsMatch(text)) return now;
        var m = Ago().Match(text);
        if (!m.Success) return null;
        var raw = m.Groups["n"].Value;
        var n = raw.StartsWith('a') ? 1 : int.Parse(raw, CultureInfo.InvariantCulture);   // "a week ago"
        return m.Groups["unit"].Value.ToLowerInvariant() switch
        {
            var u when u.StartsWith("sec") => now.AddSeconds(-n),
            var u when u.StartsWith("min") => now.AddMinutes(-n),
            var u when u.StartsWith("hour") || u == "hr" || u == "hrs" => now.AddHours(-n),
            var u when u.StartsWith("day") => now.AddDays(-n),
            var u when u.StartsWith("week") || u == "wk" || u == "wks" => now.AddDays(-7 * n),
            var u when u.StartsWith("month") || u == "mo" || u == "mos" => now.AddMonths(-n),
            var u when u.StartsWith("year") || u == "yr" || u == "yrs" => now.AddYears(-n),
            _ => null,
        };
    }

    /// <summary>"57 applicants" → 57, "Over 100 applicants" → 100, "Be among the first 25
    /// applicants" → 25 (at most), "12 people clicked apply" → 12.</summary>
    public static int? ParseApplicants(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Applicants().Match(text);
        return m.Success ? int.Parse(m.Groups["n"].Value.Replace(",", ""), CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// "$120K/yr - $150K/yr", "$120,000 - $150,000", "$60/hr - $75/hr", "$95K/yr", "$120-150K"
    /// (a K on one end applies to both), "£55K/yr", "$600/day", "$3,000/wk". A figure without a
    /// stated period is yearly when it's ≥ 1,000, hourly otherwise.
    /// </summary>
    public static (decimal? Min, decimal? Max, PayPeriod? Period) ParseSalary(string? text)
    {
        var (min, max, period, _) = ParsePay(text);
        return (min, max, period);
    }

    /// <summary><see cref="ParseSalary"/> plus the ISO currency ("USD", "GBP", "EUR").</summary>
    public static (decimal? Min, decimal? Max, PayPeriod? Period, string Currency) ParsePay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null, null, "");
        var m = MoneyRange().Match(text);
        if (!m.Success) return (null, null, null, "");

        static decimal Scale(string number, string suffix) =>
            decimal.Parse(number.Replace(",", ""), CultureInfo.InvariantCulture) *
            suffix.ToLowerInvariant() switch { "k" => 1000m, "m" => 1_000_000m, _ => 1m };

        var bSuffix = m.Groups["bk"].Value;
        var aSuffix = m.Groups["ak"].Success && m.Groups["ak"].Value.Length > 0 ? m.Groups["ak"].Value : bSuffix;
        var a = Scale(m.Groups["a"].Value, aSuffix);
        var b = m.Groups["b"].Success && m.Groups["b"].Value.Length > 0 ? Scale(m.Groups["b"].Value, bSuffix) : a;

        PayPeriod? period = null;
        var p = Period().Match(text);
        if (p.Success)
        {
            period = p.Groups["p"].Value.ToLowerInvariant() switch
            {
                "hr" or "hour" or "hourly" => PayPeriod.Hour,
                "day" or "daily" => PayPeriod.Day,
                "wk" or "week" or "weekly" => PayPeriod.Week,
                "mo" or "month" or "monthly" => PayPeriod.Month,
                _ => PayPeriod.Year,
            };
        }
        period ??= Math.Min(a, b) >= 1000m ? PayPeriod.Year : PayPeriod.Hour;
        var currency = m.Groups["cur"].Value switch { "£" => "GBP", "€" => "EUR", _ => "USD" };
        return (Math.Min(a, b), Math.Max(a, b), period, currency);
    }

    /// <summary>True for a tag that states pay.</summary>
    public static bool LooksLikeSalary(string? text) => !string.IsNullOrWhiteSpace(text) && MoneyRange().IsMatch(text);

    public static WorkplaceType ParseWorkplace(IEnumerable<string> insights)
    {
        foreach (var raw in insights)
        {
            var t = raw.Trim().ToLowerInvariant();
            if (t.StartsWith("remote")) return WorkplaceType.Remote;
            if (t.StartsWith("hybrid")) return WorkplaceType.Hybrid;
            if (t.StartsWith("on-site") || t.StartsWith("onsite") || t.StartsWith("on site")) return WorkplaceType.OnSite;
        }
        return WorkplaceType.Unknown;
    }

    public static EmploymentType ParseEmploymentType(IEnumerable<string> insights)
    {
        foreach (var raw in insights)
        {
            var t = raw.Trim().ToLowerInvariant();
            if (t.StartsWith("full-time") || t.StartsWith("full time")) return EmploymentType.FullTime;
            if (t.StartsWith("part-time") || t.StartsWith("part time")) return EmploymentType.PartTime;
            if (t.StartsWith("contract-to-hire") || t.StartsWith("contract to hire")) return EmploymentType.ContractToHire;
            if (t.StartsWith("contract")) return EmploymentType.Contract;
            if (t.StartsWith("temporary")) return EmploymentType.Temporary;
            if (t.StartsWith("internship")) return EmploymentType.Internship;
        }
        return EmploymentType.Unknown;
    }

    /// <summary>The LinkedIn job id in a URL: /jobs/view/4012345678/, /jobs/view/senior-dev-at-acme-4012345678,
    /// or ?currentJobId=4012345678.</summary>
    public static string? JobIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = JobIdInUrl().Match(url);
        return m.Success ? m.Groups["id"].Value : null;
    }

    public static string ViewUrl(string jobId, string baseUrl = LinkedInJobBoard.DefaultBaseUrl) => $"{baseUrl}/jobs/view/{jobId}/";

    /// <summary>"Senior .NET Developer Senior .NET Developer" → "Senior .NET Developer". LinkedIn
    /// repeats a title in a visually-hidden span for screen readers; textContent gets both.</summary>
    public static string Undouble(string? text)
    {
        var t = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        if (t.Length >= 2 && t.Length % 2 == 1 && t[t.Length / 2] == ' ')
        {
            var half = t[..(t.Length / 2)];
            if (t[(t.Length / 2 + 1)..] == half) return half;
        }
        return t;
    }

    [GeneratedRegex(@"\bjust now\b|\bmoments? ago\b", RegexOptions.IgnoreCase)]
    private static partial Regex JustNow();

    [GeneratedRegex(@"(?<n>\d+|\ban?\b)\+?\s*(?<unit>seconds?|secs?|minutes?|mins?|hours?|hrs?|days?|weeks?|wks?|months?|mos?|years?|yrs?)\s+ago", RegexOptions.IgnoreCase)]
    private static partial Regex Ago();

    [GeneratedRegex(@"(?<n>\d[\d,]*)\+?\s+(?:other\s+)?(?:applicants?|people clicked apply)", RegexOptions.IgnoreCase)]
    private static partial Regex Applicants();

    // A K/M suffix must touch its number and end a word — "$150,000 Matches your…" is not $150 billion.
    [GeneratedRegex(@"(?<cur>[$£€])\s?(?<a>\d[\d,]*(?:\.\d+)?)(?:(?<ak>[kKmM])\b)?(?:\s*(?:/\s?\w+)?\s*(?:-|–|—|to)\s*[$£€]?\s?(?<b>\d[\d,]*(?:\.\d+)?)(?:(?<bk>[kKmM])\b)?)?")]
    private static partial Regex MoneyRange();

    [GeneratedRegex(@"/\s?(?<p>yr|year|hr|hour|mo|month|wk|week|day)\b|\b(?<p>hourly|daily|weekly|monthly|yearly|annually)\b|per\s(?<p>hour|day|week|year|month)|\ban?\s(?<p>hour|day|week|year|month)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Period();

    [GeneratedRegex(@"(?:/jobs/view/(?:[^/?#]*-)?|[?&]currentJobId=)(?<id>\d{6,})")]
    private static partial Regex JobIdInUrl();
}
