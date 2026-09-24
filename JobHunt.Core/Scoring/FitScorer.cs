using System.Globalization;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;

namespace JobHunt.Core.Scoring;

public sealed record FitResult(bool Passed, IReadOnlyList<string> Rejections, int Score, IReadOnlyList<ScoreComponent> Breakdown);

/// <summary>
/// Decides whether a job passes the user's hard filters and how well it fits (0–100). The LLM only
/// supplies facts — which requirements the profile meets, the employment type, the salary; every
/// decision and every point is computed here, so scores are stable and each one is explainable.
/// A field the posting leaves unstated never fails a filter; it earns partial credit instead.
/// </summary>
public sealed class FitScorer
{
    public FitResult Evaluate(JobPosting job, SearchProfile search, UserProfile? profile, DateTimeOffset now)
    {
        var f = search.Filters;
        var rejections = HardFilters(job, f, profile).ToList();
        var w = search.Weights;

        var breakdown = new List<ScoreComponent>
        {
            RequirementsComponent("Required skills", w.RequiredSkills, job, RequirementKind.Required, noneListedCredit: 0.5),
            RequirementsComponent("Nice-to-have skills", w.NiceToHaveSkills, job, RequirementKind.NiceToHave, noneListedCredit: 1.0),
            TitleComponent(w.TitleAlignment, job.Title, search.SearchTerms),
            SalaryComponent(w.Salary, job, f),
            WorkplaceComponent(w.Workplace, job.Workplace, f),
            EmploymentComponent(w.EmploymentType, job.EmploymentType, f),
            FreshnessComponent(w.Freshness, job, now),
        };

        var totalWeight = breakdown.Sum(c => c.Weight);
        var score = totalWeight <= 0 ? 0 : (int)Math.Round(100 * breakdown.Sum(c => c.Earned) / totalWeight);
        return new FitResult(rejections.Count == 0, rejections, Math.Clamp(score, 0, 100), breakdown);
    }

    private static IEnumerable<string> HardFilters(JobPosting job, HuntFilters f, UserProfile? profile)
    {
        if (job.BoardShowsApplied) yield return "Already applied";
        if (f.QuickApplyOnly && !job.SupportsQuickApply) yield return "No quick apply on this posting";

        if (f.CompanyBlocklist.Any(c => Same(c, job.Company))) yield return $"Company blocked: {job.Company}";

        if (job.Workplace != WorkplaceType.Unknown && f.Workplaces.Count > 0 && !f.Workplaces.Contains(job.Workplace))
            yield return $"Workplace is {job.Workplace}";

        if (job.EmploymentType != EmploymentType.Unknown && f.EmploymentTypes.Count > 0 && !f.EmploymentTypes.Contains(job.EmploymentType))
            yield return $"Employment type is {job.EmploymentType}";

        var (_, annualMax) = Comparable(job) ? job.AnnualizedSalary() : (null, null);
        if (annualMax is null)
        {
            if (f.ExcludeUnknownSalary) yield return "No salary stated";
        }
        else
        {
            if (f.MinAnnualSalary is > 0 && annualMax < f.MinAnnualSalary)
                yield return $"Pays up to {Money(annualMax.Value)}/yr, below your {Money(f.MinAnnualSalary.Value)} minimum";
            if (f.MinHourlyRate is > 0 && annualMax / 2080m < f.MinHourlyRate)
                yield return $"Pays up to {annualMax.Value / 2080m:C0}/hr, below your {f.MinHourlyRate.Value:C0}/hr minimum";
        }

        if (f.MinContractMonths is > 0 && job.ContractMonths is { } months &&
            job.EmploymentType is EmploymentType.Contract or EmploymentType.Temporary && months < f.MinContractMonths)
            yield return $"Contract is {months} months, under your {f.MinContractMonths} month minimum";

        if (f.MaxApplicants is > 0 && job.ApplicantCount > f.MaxApplicants)
            yield return $"{job.ApplicantCount} applicants, over your limit of {f.MaxApplicants}";

        var avoided = job.TechStack.Where(t => f.MustAvoidTech.Any(a => Same(a, t))).ToList();
        if (avoided.Count > 0) yield return $"Uses {string.Join(", ", avoided)}";

        if (f.MustHaveAnyTech.Count > 0 && job.TechStack.Count > 0 && !job.TechStack.Any(t => f.MustHaveAnyTech.Any(m => Same(m, t))))
            yield return $"Stack has none of {string.Join(", ", f.MustHaveAnyTech)}";

        if (f.ExcludeStaffingAgencies && job.IsStaffingAgency == true) yield return "Staffing agency";

        if (profile != null)
        {
            if (profile.Authorization.RequiresSponsorship && job.SponsorshipAvailable == false)
                yield return "No visa sponsorship";
            if (job.ClearanceRequired == true && string.IsNullOrWhiteSpace(profile.Authorization.SecurityClearance))
                yield return "Security clearance required";
        }
    }

    private static ScoreComponent RequirementsComponent(string name, double weight, JobPosting job, RequirementKind kind, double noneListedCredit)
    {
        var reqs = job.Requirements.Where(r => r.Kind == kind).ToList();
        if (reqs.Count == 0)
            return new() { Criterion = name, Weight = weight, Earned = weight * noneListedCredit, Detail = "None listed" };
        var met = reqs.Count(r => r.IsMet);
        return new() { Criterion = name, Weight = weight, Earned = weight * met / reqs.Count, Detail = $"Matched {met}/{reqs.Count}" };
    }

    private static ScoreComponent TitleComponent(double weight, string title, IReadOnlyList<string> searchTerms)
    {
        var titleTokens = Tokens(title);
        var best = 0.0;
        var bestTerm = "";
        foreach (var term in searchTerms)
        {
            var termTokens = Tokens(term);
            if (termTokens.Count == 0) continue;
            var ratio = (double)termTokens.Count(titleTokens.Contains) / termTokens.Count;
            if (ratio > best) { best = ratio; bestTerm = term; }
        }
        return new()
        {
            Criterion = "Title match", Weight = weight, Earned = weight * best,
            Detail = best > 0 ? $"Closest to \"{bestTerm}\" ({best:P0})" : "No search term in the title",
        };
    }

    private static ScoreComponent SalaryComponent(double weight, JobPosting job, HuntFilters f)
    {
        if (!Comparable(job) && job.SalaryMax is not null)
            return new() { Criterion = "Salary", Weight = weight, Earned = weight * 0.5, Detail = $"{job.SalaryText} (not compared: {job.SalaryCurrency})" };
        var (_, max) = job.AnnualizedSalary();
        if (max is null)
            return new() { Criterion = "Salary", Weight = weight, Earned = weight * 0.5, Detail = "Not stated" };
        if (f.MinAnnualSalary is not > 0)
            return new() { Criterion = "Salary", Weight = weight, Earned = weight, Detail = $"Up to {Money(max.Value)}/yr" };

        var min = (decimal)f.MinAnnualSalary.Value;
        // At the minimum earns 75%; 25% above it (or more) earns full credit.
        var above = Math.Clamp((double)((max.Value - min) / (0.25m * min)), 0, 1);
        return new()
        {
            Criterion = "Salary", Weight = weight, Earned = weight * (0.75 + 0.25 * above),
            Detail = $"Up to {Money(max.Value)}/yr vs your {Money(min)} minimum",
        };
    }

    private static ScoreComponent WorkplaceComponent(double weight, WorkplaceType workplace, HuntFilters f)
    {
        if (workplace == WorkplaceType.Unknown)
            return new() { Criterion = "Workplace", Weight = weight, Earned = weight * 0.5, Detail = "Not stated" };
        var first = f.Workplaces.Count == 0 || f.Workplaces[0] == workplace;
        return new() { Criterion = "Workplace", Weight = weight, Earned = weight * (first ? 1 : 0.5), Detail = workplace.ToString() };
    }

    private static ScoreComponent EmploymentComponent(double weight, EmploymentType type, HuntFilters f)
    {
        if (type == EmploymentType.Unknown)
            return new() { Criterion = "Employment type", Weight = weight, Earned = weight * 0.5, Detail = "Not stated" };
        var preferred = f.PreferredEmploymentTypes.Count == 0 || f.PreferredEmploymentTypes.Contains(type);
        return new()
        {
            Criterion = "Employment type", Weight = weight, Earned = weight * (preferred ? 1 : 0.4),
            Detail = preferred ? $"{type} (preferred)" : type.ToString(),
        };
    }

    private static ScoreComponent FreshnessComponent(double weight, JobPosting job, DateTimeOffset now)
    {
        double? age = job.PostedAt is { } posted ? 1 - Math.Clamp((now - posted).TotalDays / 30, 0, 1) : null;
        double? competition = job.ApplicantCount is { } n ? 1 - Math.Clamp((n - 25) / 175.0, 0, 1) : null;
        var parts = new[] { age, competition }.Where(p => p.HasValue).Select(p => p!.Value).ToList();
        var credit = parts.Count == 0 ? 0.5 : parts.Average();
        var detail = string.Join(", ", new[]
        {
            job.PostedAt is { } p ? $"posted {Math.Max(0, (int)(now - p).TotalDays)}d ago" : null,
            job.ApplicantCount is { } c ? $"{c} applicants" : null,
        }.Where(s => s != null));
        return new() { Criterion = "Freshness", Weight = weight, Earned = weight * credit, Detail = detail.Length > 0 ? detail : "Not stated" };
    }

    /// <summary>Minimums are entered in dollars; a salary in another currency is shown, not compared.</summary>
    private static bool Comparable(JobPosting job) => job.SalaryCurrency is "" or "USD";

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Money(decimal amount) => amount >= 1000
        ? $"${Math.Round(amount / 1000m):0}k"
        : amount.ToString("C0", CultureInfo.GetCultureInfo("en-US"));

    /// <summary>Lower-cased title words. ".NET" and "C#" survive intact — splitting on punctuation
    /// would turn ".NET Developer" into "net developer" and "C#" into "c".</summary>
    internal static HashSet<string> Tokens(string? text) =>
        (text ?? "").ToLowerInvariant()
            .Split([' ', '/', ',', '-', '(', ')', '|', '–', '—', '&'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim('.', ':', ';'))
            .Select(t => t == "net" ? ".net" : t)
            .Where(t => t.Length > 0 && t is not ("and" or "or" or "the" or "of" or "a"))
            .ToHashSet();
}
