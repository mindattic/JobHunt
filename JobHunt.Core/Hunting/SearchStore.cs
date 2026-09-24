using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Data;
using JobHunt.Core.Jobs;
using JobHunt.Core.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Hunting;

/// <summary>
/// A user's job requirements — the searches a hunt runs. Every user has at least one; the first is
/// created from their profile's work preferences (desired titles, remote, minimum salary,
/// employment types) the first time it's asked for.
/// </summary>
public sealed class SearchStore(IDbContextFactory<JobHuntDb> dbFactory, ILogger<SearchStore> log)
{
    public async Task<List<SearchProfile>> ListAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Searches.AsNoTracking().Where(s => s.UserProfileId == userId).OrderBy(s => s.Id).ToListAsync(ct);
    }

    /// <summary>The user's main search, created from their profile preferences if they have none.</summary>
    public async Task<SearchProfile> GetOrCreatePrimaryAsync(UserProfile user, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Searches.AsNoTracking().Where(s => s.UserProfileId == user.Id).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (existing != null) return existing;

        var search = FromPreferences(user);
        db.Searches.Add(search);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Created job requirements for user {User} from their preferences", user.Id);
        return search;
    }

    public async Task<SearchProfile> SaveAsync(SearchProfile search, CancellationToken ct = default)
    {
        if (search.UserProfileId == 0) throw new ArgumentException("A search must belong to a user.", nameof(search));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (search.Id != 0)
        {
            // A save can never move a search to a different applicant (e.g. a form that was showing
            // one applicant's requirements when another became active).
            var owner = await db.Searches.Where(s => s.Id == search.Id).Select(s => (int?)s.UserProfileId).FirstOrDefaultAsync(ct);
            if (owner is null) search.Id = 0;
            else if (owner != search.UserProfileId)
                throw new InvalidOperationException("Those job requirements belong to a different applicant. They were not saved.");
        }
        if (search.Id == 0) db.Searches.Add(search);
        else db.Searches.Update(search);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Job requirements {Id} saved for user {User}: {Terms}", search.Id, search.UserProfileId, string.Join(", ", search.SearchTerms));
        return search;
    }

    /// <summary>Starting requirements for a user, taken from what their profile says they want.</summary>
    public static SearchProfile FromPreferences(UserProfile user)
    {
        var p = user.Preferences;
        var workplaces = new List<WorkplaceType>();
        if (p.WantsRemote) workplaces.Add(WorkplaceType.Remote);
        if (p.WantsHybrid) workplaces.Add(WorkplaceType.Hybrid);
        if (p.WantsOnSite) workplaces.Add(WorkplaceType.OnSite);
        if (workplaces.Count == 0) workplaces.Add(WorkplaceType.Remote);

        var types = new List<EmploymentType>();
        if (p.WantsFullTime) types.Add(EmploymentType.FullTime);
        if (p.WantsContract) types.Add(EmploymentType.Contract);
        if (p.WantsContractToHire) types.Add(EmploymentType.ContractToHire);
        if (p.WantsPartTime) types.Add(EmploymentType.PartTime);

        return new SearchProfile
        {
            UserProfileId = user.Id,
            Name = "My job search",
            BoardId = LinkedInJobBoard.BoardId,
            SearchTerms = [.. p.DesiredTitles],
            Filters = new HuntFilters
            {
                Workplaces = workplaces,
                EmploymentTypes = types.Count > 0 ? types : [EmploymentType.FullTime],
                PreferredEmploymentTypes = p.WantsFullTime ? [EmploymentType.FullTime] : [],
                MinAnnualSalary = user.Compensation.MinimumAnnualSalary,
                MinHourlyRate = user.Compensation.MinimumHourlyRate,
                MinContractMonths = p.MinimumContractMonths,
                CompanyBlocklist = [.. p.CompanyBlocklist],
                Location = string.IsNullOrWhiteSpace(user.Contact.Address.Country) ? "United States" : user.Contact.Address.Country,
            },
        };
    }
}
