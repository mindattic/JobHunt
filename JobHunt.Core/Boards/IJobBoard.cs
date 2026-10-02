using JobHunt.Core.Hunting;

namespace JobHunt.Core.Boards;

/// <summary>
/// One job website. Everything that knows a specific site's URLs and markup lives behind this, so
/// adding a board (Indeed, Dice, a company ATS) is a new implementation — the profile, database,
/// scoring and documents are shared and unchanged.
/// </summary>
public interface IJobBoard
{
    /// <summary>Stable id stored on every posting ("linkedin"). Never change it once shipped.</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Where the user signs in; the board's session lives in the app's persistent
    /// browser profile, never as a stored password.</summary>
    string SignInUrl { get; }

    /// <summary>Where the board pane opens — the board's jobs home.</summary>
    string HomeUrl { get; }

    /// <summary>
    /// The cookie the site keeps while an account is signed in, or null if it has none worth
    /// trusting. This is how JobHunt knows the pane is signed in however the user did it (Google,
    /// Apple, email) — far more reliable than guessing from the page's markup, which changes.
    /// </summary>
    string? SessionCookieName { get; }

    /// <summary>Name of the board's one-click application ("Easy Apply"), for UI text.</summary>
    string QuickApplyName { get; }

    /// <summary>CSS selectors for page clutter the pane hides (chat overlays, alert prompts) — see
    /// <see cref="AutoWebNav.PageDeclutter"/>. Hidden, never removed, so automation is unaffected.</summary>
    IReadOnlyList<string> HiddenClutter => [];

    /// <summary>The search-results URL for one term, one results page (0-based), with every
    /// filter this board can apply server-side.</summary>
    string BuildSearchUrl(string searchTerm, HuntFilters filters, int page = 0);
}

/// <summary>Every registered board, looked up by id.</summary>
public sealed class JobBoardRegistry(IEnumerable<IJobBoard> boards)
{
    private readonly Dictionary<string, IJobBoard> byId =
        boards.ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IJobBoard> All => byId.Values;

    public IJobBoard Get(string id) =>
        byId.TryGetValue(id, out var board)
            ? board
            : throw new KeyNotFoundException($"No job board registered with id '{id}'.");
}
