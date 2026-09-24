using JobHunt.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Profile;

/// <summary>One row of the user picker.</summary>
public sealed record UserSummary(int Id, string DisplayName, string Email, bool IsDeleted);

/// <summary>
/// The users (applicant profiles). <see cref="SaveAsync"/> takes a whole edited profile — from the
/// user form, the profile editor, or an import — and reconciles it with the stored one: children
/// with a matching id are updated in place (so the ids tailored documents cite stay stable), new
/// ones are added and missing ones removed. Deleting a user only hides it (HOUSE-LAW-2).
/// </summary>
public sealed class ProfileStore(IDbContextFactory<JobHuntDb> dbFactory, TimeProvider clock, ILogger<ProfileStore> log)
{
    /// <summary>Users for the picker, alphabetical. Deleted users only when asked for.</summary>
    public async Task<List<UserSummary>> ListAsync(bool includeDeleted = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Profiles.AsNoTracking()
            .Where(p => includeDeleted || p.DeletedAt == null)
            .Select(p => new { p.Id, p.Contact.FirstName, p.Contact.LastName, p.Contact.Email, p.DeletedAt })
            .ToListAsync(ct);
        return rows
            .Select(r =>
            {
                var probe = new UserProfile { Contact = { FirstName = r.FirstName, LastName = r.LastName, Email = r.Email } };
                return new UserSummary(r.Id, probe.DisplayName, r.Email, r.DeletedAt != null);
            })
            .OrderBy(u => u.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The user with everything loaded — every list in the order the user gave it — or
    /// null when there is none with that id.</summary>
    public async Task<UserProfile?> LoadAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await WithEverything(db.Profiles).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        Order(p.BoardAccounts); Order(p.Links); Order(p.Experience); Order(p.Education); Order(p.Skills);
        Order(p.Certifications); Order(p.Languages); Order(p.Projects); Order(p.Accomplishments); Order(p.References);
        Order(p.KeywordLists); Order(p.TextBlocks); Order(p.CustomFields); Order(p.Answers);
        foreach (var e in p.Experience) Order(e.Bullets);
        return p;
    }

    private static void Order<T>(List<T> list) where T : IOrdered
    {
        var sorted = list.OrderBy(x => x.SortOrder).ToList();
        list.Clear();
        list.AddRange(sorted);
    }

    /// <summary>The order the user arranged a list in is its position in the list.</summary>
    private static void Number<T>(List<T> list) where T : IOrdered
    {
        for (var i = 0; i < list.Count; i++) list[i].SortOrder = i;
    }

    private static void NumberAll(UserProfile p)
    {
        Number(p.BoardAccounts); Number(p.Links); Number(p.Experience); Number(p.Education); Number(p.Skills);
        Number(p.Certifications); Number(p.Languages); Number(p.Projects); Number(p.Accomplishments); Number(p.References);
        Number(p.KeywordLists); Number(p.TextBlocks); Number(p.CustomFields); Number(p.Answers);
        foreach (var e in p.Experience) Number(e.Bullets);
    }

    /// <summary>Creates the user when <see cref="UserProfile.Id"/> is 0, otherwise updates it.</summary>
    public async Task<UserProfile> SaveAsync(UserProfile incoming, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        incoming.UpdatedAt = now;
        foreach (var a in incoming.Answers) a.NormalizedQuestion = ScreeningAnswer.Normalize(a.Question);
        NumberAll(incoming);

        var stored = incoming.Id == 0 ? null
            : await WithEverything(db.Profiles).FirstOrDefaultAsync(p => p.Id == incoming.Id, ct);
        if (stored is null)
        {
            if (incoming.Id != 0) throw new KeyNotFoundException($"No user with id {incoming.Id}.");
            ClearIds(incoming);
            incoming.CreatedAt = now;
            db.Profiles.Add(incoming);
            await db.SaveChangesAsync(ct);
            log.LogInformation("User {Id} created: {Name}", incoming.Id, incoming.DisplayName);
            return incoming;
        }

        stored.Contact = incoming.Contact;
        stored.Authorization = incoming.Authorization;
        stored.Compensation = incoming.Compensation;
        stored.Preferences = incoming.Preferences;
        stored.Summary = incoming.Summary;
        stored.Disclosures = incoming.Disclosures;
        stored.UpdatedAt = now;

        Sync(db, stored.BoardAccounts, incoming.BoardAccounts, x => x.Id);
        Sync(db, stored.Links, incoming.Links, x => x.Id);
        Sync(db, stored.Education, incoming.Education, x => x.Id);
        Sync(db, stored.Skills, incoming.Skills, x => x.Id);
        Sync(db, stored.Certifications, incoming.Certifications, x => x.Id);
        Sync(db, stored.Languages, incoming.Languages, x => x.Id);
        Sync(db, stored.Projects, incoming.Projects, x => x.Id);
        Sync(db, stored.Accomplishments, incoming.Accomplishments, x => x.Id);
        Sync(db, stored.References, incoming.References, x => x.Id);
        Sync(db, stored.KeywordLists, incoming.KeywordLists, x => x.Id);
        Sync(db, stored.TextBlocks, incoming.TextBlocks, x => x.Id);
        Sync(db, stored.CustomFields, incoming.CustomFields, x => x.Id);
        Sync(db, stored.Answers, incoming.Answers, x => x.Id);
        Sync(db, stored.Experience, incoming.Experience, x => x.Id,
            (kept, edit) => Sync(db, kept.Bullets, edit.Bullets, b => b.Id),
            fresh => { foreach (var b in fresh.Bullets) b.Id = 0; });

        await db.SaveChangesAsync(ct);
        log.LogInformation("User {Id} saved: {Name}", stored.Id, stored.DisplayName);
        return stored;
    }

    /// <summary>Hides the user. Their profile, searches, jobs and application history are kept.</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) => SetDeletedAsync(id, clock.GetUtcNow(), ct);

    public Task RestoreAsync(int id, CancellationToken ct = default) => SetDeletedAsync(id, null, ct);

    private async Task SetDeletedAsync(int id, DateTimeOffset? at, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var changed = await db.Profiles.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, at), ct);
        if (changed == 0) throw new KeyNotFoundException($"No user with id {id}.");
        log.LogInformation("User {Id} {Action}", id, at is null ? "restored" : "deleted (hidden)");
    }

    /// <summary>Adds the profile in an exported file as a NEW user.</summary>
    public Task<UserProfile> ImportAsync(string json, CancellationToken ct = default)
    {
        var imported = ProfileTransfer.Import(json);
        ClearIds(imported);
        imported.DeletedAt = null;
        log.LogInformation("Importing a profile as a new user: {Name}", imported.DisplayName);
        return SaveAsync(imported, ct);
    }

    public async Task<string> ExportAsync(int id, CancellationToken ct = default)
    {
        var profile = await LoadAsync(id, ct) ?? throw new KeyNotFoundException($"No user with id {id}.");
        profile.DeletedAt = null;
        return ProfileTransfer.Export(profile, clock.GetUtcNow());
    }

    internal static IQueryable<UserProfile> WithEverything(IQueryable<UserProfile> q) => q
        .Include(p => p.BoardAccounts).Include(p => p.Links).Include(p => p.Experience).ThenInclude(e => e.Bullets)
        .Include(p => p.Education).Include(p => p.Skills).Include(p => p.Certifications)
        .Include(p => p.Languages).Include(p => p.Projects).Include(p => p.Accomplishments)
        .Include(p => p.References).Include(p => p.KeywordLists).Include(p => p.TextBlocks)
        .Include(p => p.CustomFields).Include(p => p.Answers)
        .AsSplitQuery();

    /// <summary>Makes <paramref name="stored"/> match <paramref name="incoming"/>: same-id rows get
    /// the incoming values, id-0 or unknown-id rows are inserted, rows not present are deleted.
    /// <paramref name="resetNewChildren"/> clears the ids inside an inserted row's own children —
    /// otherwise a new job entry carrying another row's bullet ids would UPDATE those bullets
    /// (even another applicant's) instead of inserting its own. A second copy of a row already
    /// kept (a duplicated entry) is inserted too, never double-tracked.</summary>
    private static void Sync<T>(JobHuntDb db, List<T> stored, List<T> incoming, Func<T, int> id,
        Action<T, T>? children = null, Action<T>? resetNewChildren = null)
        where T : class
    {
        var byId = stored.ToDictionary(id);
        var keep = new HashSet<int>();
        var result = new List<T>();
        foreach (var edit in incoming)
        {
            if (id(edit) != 0 && !keep.Contains(id(edit)) && byId.TryGetValue(id(edit), out var existing))
            {
                db.Entry(existing).CurrentValues.SetValues(edit);
                children?.Invoke(existing, edit);
                keep.Add(id(edit));
                result.Add(existing);
            }
            else
            {
                SetId(edit, 0);
                resetNewChildren?.Invoke(edit);
                result.Add(edit);
            }
        }
        foreach (var gone in stored.Where(s => !keep.Contains(id(s)))) db.Remove(gone);
        stored.Clear();
        stored.AddRange(result);
    }

    private static void SetId<T>(T entity, int value) =>
        typeof(T).GetProperty("Id")?.SetValue(entity, value);

    internal static void ClearIds(UserProfile p)
    {
        p.Id = 0;
        foreach (var x in p.BoardAccounts) x.Id = 0;
        foreach (var x in p.Links) x.Id = 0;
        foreach (var x in p.Experience) { x.Id = 0; foreach (var b in x.Bullets) b.Id = 0; }
        foreach (var x in p.Education) x.Id = 0;
        foreach (var x in p.Skills) x.Id = 0;
        foreach (var x in p.Certifications) x.Id = 0;
        foreach (var x in p.Languages) x.Id = 0;
        foreach (var x in p.Projects) x.Id = 0;
        foreach (var x in p.Accomplishments) x.Id = 0;
        foreach (var x in p.References) x.Id = 0;
        foreach (var x in p.KeywordLists) x.Id = 0;
        foreach (var x in p.TextBlocks) x.Id = 0;
        foreach (var x in p.CustomFields) x.Id = 0;
        foreach (var x in p.Answers) x.Id = 0;
    }
}
