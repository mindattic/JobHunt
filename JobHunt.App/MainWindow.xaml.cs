using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using AutoWebNav;
using AutoWebNav.WebView2;
using JobHunt.Core.Boards;
using JobHunt.Core.Boards.LinkedIn;
using JobHunt.Core.Data;
using JobHunt.Core.Hunting;
using JobHunt.Core.Jobs;
using JobHunt.Core.Llm;
using JobHunt.Core.Profile;
using JobHunt.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace JobHunt.App;

/// <summary>
/// Hosts the panel, the job-board pane and the native command bar. The panel talks to this window
/// over WebView2 postMessage: it sends <c>{ action, ... }</c>, this sends back <c>{ type, ... }</c>.
/// Everything shown is for the active applicant; switching applicants switches the profile, job
/// requirements, best fits, application log — and the board pane itself, since each applicant
/// has their own browser profile (and so their own LinkedIn session).
/// </summary>
public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly JobHuntPaths paths = App.Services.GetRequiredService<JobHuntPaths>();
    private readonly JobStore jobs = App.Services.GetRequiredService<JobStore>();
    private readonly ProfileStore profiles = App.Services.GetRequiredService<ProfileStore>();
    private readonly SearchStore searches = App.Services.GetRequiredService<SearchStore>();
    private readonly SettingsStore settings = App.Services.GetRequiredService<SettingsStore>();
    private readonly ByokKeys keys = App.Services.GetRequiredService<ByokKeys>();
    private readonly BoardPasswords passwords = App.Services.GetRequiredService<BoardPasswords>();
    private readonly LinkedInSignIn linkedInSignIn = App.Services.GetRequiredService<LinkedInSignIn>();
    private readonly DatabaseTransfer transfer = App.Services.GetRequiredService<DatabaseTransfer>();
    private readonly HuntRunner hunter = App.Services.GetRequiredService<HuntRunner>();
    private readonly IJobBoard board = App.Services.GetRequiredService<JobBoardRegistry>().Get(LinkedInJobBoard.BoardId);
    private readonly ILogger<MainWindow> log = App.Services.GetRequiredService<ILogger<MainWindow>>();

    /// <summary>The live job-board page for the active applicant, for the search and apply engines.</summary>
    private IBrowserSurface? boardSurface;
    private WebView2? boardView;
    private int? boardUserId;
    private int selectedCount;
    private CancellationTokenSource? huntCts;
    /// <summary>True from the moment a hunt is asked for (sign-in included) until it has fully ended —
    /// the lock that keeps a second hunt, an applicant switch, or an import from pulling the page
    /// out from under it.</summary>
    private bool huntBusy;
    private bool Hunting => huntBusy;

    private int? ActiveUserId => settings.Current.ActiveProfileId;

    public MainWindow()
    {
        InitializeComponent();
        ApplyDryRunBadge();
        _ = InitializePanelAsync();
#if DEBUG
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F11) Panel.CoreWebView2?.OpenDevToolsWindow();
            if (e.Key == System.Windows.Input.Key.F10) boardView?.CoreWebView2?.OpenDevToolsWindow();
        };
#endif
    }

    // ── panes ──────────────────────────────────────────────────────────────────────────────────

    private async Task InitializePanelAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: paths.PanelProfileDirectory);
        await Panel.EnsureCoreWebView2Async(env);
        Panel.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "jobhunt.local", Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Allow);
        Panel.CoreWebView2.WebMessageReceived += OnPanelMessage;
        Panel.CoreWebView2.Navigate("https://jobhunt.local/panel.html");
    }

    /// <summary>
    /// Shows the board pane for <paramref name="userId"/>. A WebView2's browser profile is fixed when
    /// it is created, so a different applicant means a new WebView2 over that applicant's own
    /// profile folder — never someone else's LinkedIn session.
    /// </summary>
    private async Task ShowBoardForAsync(int? userId)
    {
        // One switch at a time: tearing a WebView2 down while it is still initializing aborts it.
        await boardGate.WaitAsync();
        try
        {
            if (!await SwapBoardAsync(userId)) return;
        }
        finally
        {
            boardGate.Release();
        }
        // Outside the gate: a sign-in can take many seconds and must not hold up switching again.
        if (userId is { } signedInFor) await AutoSignInAsync(signedInFor);
    }

    private readonly SemaphoreSlim boardGate = new(1, 1);

    /// <summary>Replaces the board pane; false when it was already showing that applicant.</summary>
    private async Task<bool> SwapBoardAsync(int? userId)
    {
        if (userId == boardUserId && (boardView != null || userId is null)) return false;

        if (boardView != null)
        {
            BoardHost.Children.Remove(boardView);
            boardView.Dispose();
            boardView = null;
            boardSurface = null;
        }
        boardUserId = userId;
        boardSignedIn = null;
        UpdateHuntButton();
        BoardPlaceholder.Visibility = userId is null ? Visibility.Visible : Visibility.Collapsed;
        if (userId is not { } id) return false;

        var view = new WebView2();
        BoardHost.Children.Add(view);
        boardView = view;

        var folder = paths.BrowserProfileDirectory(id);
        Directory.CreateDirectory(folder);
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
            await view.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            // Leave nothing half-made: the next switch to this applicant tries again from scratch.
            log.LogError(ex, "The {Board} pane failed to start for user {User}", board.DisplayName, id);
            BoardHost.Children.Remove(view);
            view.Dispose();
            boardView = null;
            boardUserId = null;
            UpdateHuntButton();
            await ToastAsync($"{board.DisplayName} couldn't start: {ex.Message}", error: true);
            return false;
        }

        // Popups would escape automation; keep every navigation in this pane.
        view.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            view.CoreWebView2.Navigate(args.Uri);
        };
        // A native dialog would block the renderer and hang every script call behind it.
        view.CoreWebView2.ScriptDialogOpening += (_, args) =>
        {
            log.LogWarning("Board page raised a {Kind} dialog: {Message} — auto-accepting", args.Kind, args.Message);
            args.Accept();
        };

        var surface = new WebView2BrowserSurface(view.CoreWebView2,
            factor => Dispatcher.Invoke(() => view.ZoomFactor = factor));
        await surface.EnsureInstalledAsync();
        boardSurface = surface;
        // Tell the Applicant tab whether this pane is signed in, every time it lands on a page —
        // that's how a Google/Apple sign-in done by hand shows up as "Signed in".
        view.CoreWebView2.NavigationCompleted += async (_, _) => await PushBoardStatusAsync(surface);

        await surface.NavigateAsync(board.HomeUrl, CancellationToken.None);
        log.LogInformation("{Board} pane ready for user {User}", board.DisplayName, id);
        UpdateHuntButton();
        return true;
    }

    /// <summary>Signs the applicant in with their saved account when the pane is signed out and
    /// auto sign-in is on. Challenges (codes, CAPTCHAs) are always left to the person.</summary>
    private async Task AutoSignInAsync(int userId, bool force = false)
    {
        if (boardSurface is null || boardUserId != userId)
        {
            if (force) await ToastAsync($"{board.DisplayName} is still loading. Try again in a moment.", error: true);
            return;
        }
        var user = await profiles.LoadAsync(userId);
        var account = user?.BoardAccounts.FirstOrDefault(a => a.BoardId == board.Id);
        if (account is null || (!account.AutoSignIn && !force)) return;

        if (Hunting && !force) return;
        var surface = boardSurface;
        try { if (!force && await IsBoardSignedInAsync(surface)) return; }
        catch (Exception ex) when (!ReferenceEquals(surface, boardSurface)) { log.LogDebug(ex, "Pane replaced while checking sign-in"); return; }
        SignInResult result;
        try
        {
            result = await linkedInSignIn.EnsureSignedInAsync(
                surface, board.SignInUrl, account.SignInEmail, passwords.Get(userId, board.Id), CancellationToken.None);
        }
        catch (Exception ex) when (!ReferenceEquals(surface, boardSurface))
        {
            // The applicant was switched mid-sign-in and this pane is gone — nothing to report.
            log.LogDebug(ex, "Sign-in abandoned: the board pane was replaced");
            return;
        }
        if (result.Status != SignInStatus.AlreadySignedIn || force)
            await ToastAsync(result.Message, error: result.Status is SignInStatus.Failed or SignInStatus.NeedsYou);
    }

    // ── panel bridge ───────────────────────────────────────────────────────────────────────────

    private async void OnPanelMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(args.WebMessageAsJson); }
        catch (JsonException) { return; }
        var action = msg?["action"]?.GetValue<string>();

        try
        {
            switch (action)
            {
                case "ready":
                    await EnsureActiveUserIsValidAsync();
                    await PushSettingsAsync();
                    await PushEverythingForActiveUserAsync();
                    break;

                // applicants
                case "switchUser":
                    await SwitchUserAsync(msg!["id"]!.GetValue<int>());
                    break;
                case "saveProfile":
                    await SaveProfileAsync(msg!["profile"]!);
                    break;
                case "deleteUser":
                    await DeleteUserAsync(msg!["id"]!.GetValue<int>());
                    break;
                case "restoreUser":
                    if (Hunting) { await ToastAsync("Stop the hunt before restoring an applicant.", error: true); break; }
                    var restored = msg!["id"]!.GetValue<int>();
                    await profiles.RestoreAsync(restored);
                    if (ActiveUserId is null) { SetActiveUser(restored); await PushEverythingForActiveUserAsync(); }
                    else await PushUsersAsync();
                    await ToastAsync("Applicant restored.");
                    break;
                case "saveBoardPassword":
                    SaveBoardPassword(msg!["boardId"]!.GetValue<string>(), msg["password"]!.GetValue<string>());
                    await PushProfileAsync();
                    await ToastAsync("Password saved, encrypted to your Windows account.");
                    break;
                case "forgetBoardPassword":
                    if (ActiveUserId is { } forgetFor) passwords.Clear(forgetFor, msg!["boardId"]!.GetValue<string>());
                    await PushProfileAsync();
                    await ToastAsync("Password forgotten.");
                    break;
                case "openSignIn":
                    if (Hunting) { await ToastAsync("The hunt is using the LinkedIn pane. Stop it first.", error: true); break; }
                    if (boardView?.CoreWebView2 is { } pane) pane.Navigate(board.SignInUrl);
                    else await ToastAsync($"{board.DisplayName} is still loading. Try again in a moment.", error: true);
                    break;
                case "signInNow":
                    if (Hunting) { await ToastAsync("The hunt is using the LinkedIn pane. Stop it first.", error: true); break; }
                    if (ActiveUserId is { } signInFor) await AutoSignInAsync(signInFor, force: true);
                    break;

                // job requirements
                case "saveAndHunt":
                    // One message, in order: the hunt must see the requirements just saved.
                    if (Hunting) break;
                    await SaveSearchAsync(msg!["search"]!, preview: false, quiet: true);
                    await StartHuntAsync();
                    break;
                case "stopHunt":
                    huntCts?.Cancel();
                    break;
                case "saveSearch":
                    await SaveSearchAsync(msg!["search"]!, preview: false);
                    break;
                case "previewSearch":
                    await SaveSearchAsync(msg!["search"]!, preview: true);
                    break;

                // best fits
                case "select":
                    if (ActiveUserId is not { } selectFor) break;
                    // A selection made while another applicant was showing is not this applicant's.
                    if (msg!["userId"]?.GetValue<int>() is { } madeFor && madeFor != selectFor) break;
                    var ids = msg["jobIds"]!.AsArray().Select(n => n!.GetValue<int>()).ToList();
                    SetSelectedCount(await jobs.SetSelectedAsync(selectFor, ids));
                    break;
                case "openDocument":
                    OpenDocument(msg!["path"]!.GetValue<string>());
                    break;
                case "openPosting":
                    var url = msg!["url"]!.GetValue<string>();
                    // Only the board's own site opens in the (signed-in) board pane. Compare the
                    // parsed host: a prefix check would let "linkedin.com.evil.example" through.
                    if (IsBoardUrl(url)) boardView?.CoreWebView2?.Navigate(url);
                    break;

                // settings
                case "saveSettings":
                    SaveSettings(msg!["settings"]!);
                    await PushSettingsAsync();
                    break;
                case "saveKeys":
                    var provider = msg!["provider"]!.GetValue<string>();
                    if (!LlmProviders.IsKnown(provider)) break;
                    var added = keys.AddOwnKeys(provider, msg["keys"]!.AsArray().Select(n => n!.GetValue<string>()));
                    log.LogInformation("BYO key pool for {Provider}: {Added} key(s) added", provider, added);
                    await PushSettingsAsync();
                    await ToastAsync(added == 0 ? "No new key to add." : added == 1 ? "Key added." : $"{added} keys added.", error: added == 0);
                    break;
                case "exportProfile":
                    await ExportProfileAsync();
                    break;
                case "importProfile":
                    if (Hunting) { await ToastAsync("Stop the hunt before importing.", error: true); break; }
                    await ImportProfileAsync();
                    break;
                case "exportBackup":
                    await ExportBackupAsync();
                    break;
                case "importBackup":
                    if (Hunting) { await ToastAsync("Stop the hunt before importing a backup.", error: true); break; }
                    await ImportBackupAsync();
                    break;
                case "clearKeys":
                    var clearFor = msg!["provider"]!.GetValue<string>();
                    if (!LlmProviders.IsKnown(clearFor)) break;
                    keys.SetOwnKeys(clearFor, []);
                    log.LogInformation("BYO key pool cleared for {Provider}", clearFor);
                    await PushSettingsAsync();
                    await ToastAsync("Keys removed.");
                    break;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Panel action {Action} failed", action);
            await ToastAsync($"That didn't work: {ex.Message}", error: true);
        }
    }

    /// <summary>
    /// Drops null-valued properties from a form's JSON. A cleared number box sends null, and a
    /// non-nullable property (a count, a weight) can't hold one — without it, the model's default
    /// applies instead of the save failing.
    /// </summary>
    private static JsonNode WithoutNulls(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Where(p => p.Value is null).Select(p => p.Key).ToList()) obj.Remove(key);
                foreach (var (_, value) in obj) WithoutNulls(value!);
                break;
            case JsonArray arr:
                foreach (var item in arr) if (item != null) WithoutNulls(item);
                break;
        }
        return node;
    }

    private bool IsBoardUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var target) && Uri.TryCreate(board.HomeUrl, UriKind.Absolute, out var home)
        && target.Scheme == home.Scheme && string.Equals(target.Host, home.Host, StringComparison.OrdinalIgnoreCase)
        && target.Port == home.Port;

    private Task PostAsync(object message)
    {
        Panel.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message, Json));
        return Task.CompletedTask;
    }

    private Task ToastAsync(string text, bool error = false) => PostAsync(new { type = "toast", text, error });

    // ── applicants ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Falls back to the first applicant when the saved active one is gone or deleted.</summary>
    private async Task EnsureActiveUserIsValidAsync()
    {
        var users = await profiles.ListAsync();
        if (ActiveUserId is { } id && users.Any(u => u.Id == id)) return;
        SetActiveUser(users.FirstOrDefault()?.Id);
    }

    private void SetActiveUser(int? id)
    {
        var s = settings.Current;
        if (s.ActiveProfileId == id) return;
        s.ActiveProfileId = id;
        settings.Save(s);
        log.LogInformation("Active applicant is now {User}", id?.ToString() ?? "none");
    }

    private async Task SwitchUserAsync(int id)
    {
        if (Hunting) { await ToastAsync("Stop the hunt before switching applicants.", error: true); await PushUsersAsync(); return; }
        if ((await profiles.ListAsync()).All(u => u.Id != id)) return;
        SetActiveUser(id);
        await PushEverythingForActiveUserAsync();
    }

    private async Task PushEverythingForActiveUserAsync()
    {
        await PushUsersAsync();
        await PushProfileAsync();
        await PushSearchAsync();
        await PushJobsAsync();
        await PushApplicationLogAsync();
        await ShowBoardForAsync(ActiveUserId);
    }

    private async Task PushUsersAsync()
    {
        var all = await profiles.ListAsync(includeDeleted: true);
        await PostAsync(new
        {
            type = "users",
            activeId = ActiveUserId,
            users = all.Where(u => !u.IsDeleted),
            deleted = all.Where(u => u.IsDeleted),
        });
    }

    /// <summary>Last known signed-in state of the active applicant's pane (null until checked).</summary>
    private bool? boardSignedIn;

    private async Task PushBoardStatusAsync(IBrowserSurface surface)
    {
        if (!ReferenceEquals(surface, boardSurface) || Hunting) return;   // a replaced pane, or the hunt owns it
        try { boardSignedIn = await IsBoardSignedInAsync(surface); }
        catch (Exception ex) { log.LogDebug(ex, "Couldn't read the pane's sign-in state"); return; }
        await PostAsync(new { type = "boardStatus", boardId = board.Id, signedIn = boardSignedIn });
    }

    /// <summary>
    /// Is the active pane signed in? First the site's own session cookie (true however the user
    /// signed in — Google, Apple, email), then the page itself. A cookie the site has since
    /// rejected is caught later anyway: the results page shows the sign-in wall and the hunt stops.
    /// </summary>
    private async Task<bool> IsBoardSignedInAsync(IBrowserSurface surface)
    {
        if (board.SessionCookieName is { } name && boardView?.CoreWebView2 is { } core)
        {
            var cookies = await core.CookieManager.GetCookiesAsync(board.HomeUrl);
            if (cookies.Any(c => c.Name == name && c.Value.Length > 0 && (c.IsSession || c.Expires > DateTime.Now)))
                return true;
        }
        return await linkedInSignIn.IsSignedInAsync(surface, CancellationToken.None);
    }

    private async Task PushProfileAsync()
    {
        var user = ActiveUserId is { } id ? await profiles.LoadAsync(id) : null;
        await PostAsync(new
        {
            type = "profile",
            profile = user,
            passwordSaved = user is null ? new Dictionary<string, bool>()
                : new Dictionary<string, bool> { [board.Id] = passwords.Has(user.Id, board.Id) },
            boards = new[] { new { id = board.Id, name = board.DisplayName, signInUrl = board.SignInUrl } },
        });
        await PostAsync(new { type = "boardStatus", boardId = board.Id, signedIn = boardSignedIn });
    }

    private async Task SaveProfileAsync(JsonNode node)
    {
        var incoming = WithoutNulls(node).Deserialize<UserProfile>(Json) ?? throw new InvalidOperationException("The form sent no profile.");
        var isNew = incoming.Id == 0;
        if (isNew && Hunting) { await ToastAsync("Stop the hunt before adding an applicant.", error: true); return; }
        if (!isNew && incoming.Id != ActiveUserId)
        {
            // The form was for someone who is no longer the active applicant — don't save it onto them silently.
            await ToastAsync("That form was for a different applicant. Nothing was saved; the current applicant is shown.", error: true);
            await PushProfileAsync();
            return;
        }
        var saved = await profiles.SaveAsync(incoming);
        if (isNew) SetActiveUser(saved.Id);
        await ToastAsync(isNew ? $"Applicant {saved.DisplayName} added." : "Applicant saved.");
        if (isNew) await PushEverythingForActiveUserAsync();
        else
        {
            await PushUsersAsync();
            await PushProfileAsync();
        }
    }

    private async Task DeleteUserAsync(int id)
    {
        if (Hunting) { await ToastAsync("Stop the hunt before deleting an applicant.", error: true); return; }
        var user = await profiles.LoadAsync(id);
        if (user is null) return;
        var confirm = MessageBox.Show(this,
            $"Delete {user.DisplayName}?\n\nTheir profile, job requirements, jobs and application history are kept, hidden, and can be restored from Settings.",
            "Delete applicant", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        await profiles.DeleteAsync(id);
        if (ActiveUserId == id) SetActiveUser((await profiles.ListAsync()).FirstOrDefault()?.Id);
        await PushEverythingForActiveUserAsync();
        await ToastAsync($"{user.DisplayName} deleted. Restore them from Settings if you need to.");
    }

    private void SaveBoardPassword(string boardId, string password)
    {
        if (ActiveUserId is not { } id) throw new InvalidOperationException("Save the applicant before adding a password.");
        passwords.Set(id, boardId, password);
        log.LogInformation("Saved a {Board} password for user {User}", boardId, id);
    }

    // ── job requirements ───────────────────────────────────────────────────────────────────────

    private async Task PushSearchAsync()
    {
        var user = ActiveUserId is { } id ? await profiles.LoadAsync(id) : null;
        var search = user is null ? null : await searches.GetOrCreatePrimaryAsync(user);
        await PostAsync(new { type = "search", search, preview = search is null ? null : PreviewUrls(search) });
    }

    private async Task SaveSearchAsync(JsonNode node, bool preview, bool quiet = false)
    {
        if (ActiveUserId is not { } userId) return;
        var search = WithoutNulls(node).Deserialize<SearchProfile>(Json) ?? throw new InvalidOperationException("The form sent no requirements.");
        if (node["userProfileId"]?.GetValue<int>() is { } formFor && formFor != 0 && formFor != userId)
        {
            await ToastAsync("That form was for a different applicant. Nothing was saved; the current requirements are shown.", error: true);
            await PushSearchAsync();
            return;
        }
        search.UserProfileId = userId;
        search.SearchTerms = search.SearchTerms.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();
        var saved = await searches.SaveAsync(search);
        await PostAsync(new { type = "search", search = saved, preview = PreviewUrls(saved) });

        if (preview && boardView?.CoreWebView2 is null)
            await ToastAsync($"{board.DisplayName} is still loading. Try again in a moment.", error: true);
        else if (preview && saved.SearchTerms.Count > 0)
        {
            boardView?.CoreWebView2?.Navigate(board.BuildSearchUrl(saved.SearchTerms[0], saved.Filters));
            await ToastAsync($"Showing \"{saved.SearchTerms[0]}\" on {board.DisplayName}.");
        }
        else if (!preview && !quiet) await ToastAsync("Job requirements saved.");
    }

    private object PreviewUrls(SearchProfile search) =>
        search.SearchTerms.Select(t => new { term = t, url = board.BuildSearchUrl(t, search.Filters) });

    // ── best fits and the application log ──────────────────────────────────────────────────────

    private async Task PushJobsAsync()
    {
        var recommended = ActiveUserId is { } id ? await jobs.RecommendationsAsync(id) : [];
        SetSelectedCount(recommended.Count(j => j.SelectedForApply));
        StatusText.Text = ActiveUserId is null ? "Add an applicant to get started."
            : recommended.Count == 0 ? $"No best fits yet. Set your job requirements, and sign in to {board.DisplayName} on the right."
            : $"{recommended.Count} best fits";
        await PostAsync(new { type = "jobs", jobs = recommended.Select(JobCard.From) });
    }

    private async Task PushApplicationLogAsync()
    {
        var applications = ActiveUserId is { } id ? await jobs.ApplicationLogAsync(id) : [];
        await PostAsync(new
        {
            type = "applications",
            applications = applications.Select(a => new
            {
                a.Id, a.AppliedAt, a.IsDryRun, a.Method, a.Outcome, a.OutcomeUpdatedAt,
                job = new { a.JobPosting.Title, a.JobPosting.Company, a.JobPosting.Url },
                events = a.Events.OrderBy(e => e.At).Select(e => new { e.At, e.Outcome, e.Source, e.Note }),
                a.RunLog,
            }),
        });
    }

    // ── settings ───────────────────────────────────────────────────────────────────────────────

    private Task PushSettingsAsync()
    {
        var s = settings.Current;
        ApplyDryRunBadge();
        return PostAsync(new
        {
            type = "settings",
            settings = new { s.SelectedLlmProvider, s.DryRun, s.DailyApplyCap, s.PreflightQuestionScan },
            providers = LlmProviders.All.Select(p => new
            {
                p.Id, p.DisplayName,
                ownKeyCount = keys.GetOwnKeys(p.Id).Count,
                hasSharedKey = keys.HasSharedKey(p.Id),
            }),
        });
    }

    private void SaveSettings(JsonNode node)
    {
        var s = settings.Current;
        var provider = node["selectedLlmProvider"]?.GetValue<string>();
        if (LlmProviders.IsKnown(provider)) s.SelectedLlmProvider = provider!;
        if (node["dryRun"] is { } dry) s.DryRun = dry.GetValue<bool>();
        if (node["dailyApplyCap"] is { } cap) s.DailyApplyCap = Math.Clamp(cap.GetValue<int>(), 1, 200);
        if (node["preflightQuestionScan"] is { } scan) s.PreflightQuestionScan = scan.GetValue<bool>();
        settings.Save(s);
        log.LogInformation("Settings saved: provider {Provider}, dry run {DryRun}, daily cap {Cap}",
            s.SelectedLlmProvider, s.DryRun, s.DailyApplyCap);
    }

    // ── import / export ────────────────────────────────────────────────────────────────────────

    private async Task ExportProfileAsync()
    {
        if (ActiveUserId is not { } id) return;
        var p = await profiles.LoadAsync(id);
        var dialog = new SaveFileDialog
        {
            Title = "Export this applicant",
            FileName = (p!.FullName.Length > 0 ? p.FullName.Replace(' ', '-') : "applicant") + ProfileTransfer.FileExtension,
            Filter = $"JobHunt profile (*{ProfileTransfer.FileExtension})|*{ProfileTransfer.FileExtension}|JSON (*.json)|*.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        await File.WriteAllTextAsync(dialog.FileName, await profiles.ExportAsync(id), new System.Text.UTF8Encoding(false));
        log.LogInformation("Profile of user {User} exported to {Path}", id, dialog.FileName);
        await ToastAsync($"Exported to {Path.GetFileName(dialog.FileName)}.");
    }

    private async Task ImportProfileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import an applicant",
            Filter = $"JobHunt profile (*{ProfileTransfer.FileExtension};*.json)|*{ProfileTransfer.FileExtension};*.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        var imported = await profiles.ImportAsync(await File.ReadAllTextAsync(dialog.FileName));
        log.LogInformation("Profile imported from {Path} as user {User}", dialog.FileName, imported.Id);
        SetActiveUser(imported.Id);
        await PushEverythingForActiveUserAsync();
        await ToastAsync($"Imported {imported.DisplayName} as a new applicant.");
    }

    private async Task ExportBackupAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export everything JobHunt holds",
            FileName = $"jobhunt-{DateTime.Now:yyyy-MM-dd}{DatabaseTransfer.FileExtension}",
            Filter = $"JobHunt backup (*{DatabaseTransfer.FileExtension})|*{DatabaseTransfer.FileExtension}",
        };
        if (dialog.ShowDialog(this) != true) return;
        await File.WriteAllTextAsync(dialog.FileName, await transfer.ExportAsync(), new System.Text.UTF8Encoding(false));
        log.LogInformation("Database exported to {Path}", dialog.FileName);
        await ToastAsync($"Exported to {Path.GetFileName(dialog.FileName)}.");
    }

    private async Task ImportBackupAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import a JobHunt backup",
            Filter = $"JobHunt backup (*{DatabaseTransfer.FileExtension};*.json)|*{DatabaseTransfer.FileExtension};*.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        var json = await File.ReadAllTextAsync(dialog.FileName);
        DatabaseTransfer.Parse(json);   // reject a wrong file before asking anything
        var confirm = MessageBox.Show(this,
            "Importing replaces EVERYTHING in JobHunt (every applicant, their requirements, jobs and application history) with the contents of this backup.\n\n" +
            "Export a backup of what's here first if you might want it back.\n\nReplace everything?",
            "Import backup", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        await transfer.ImportAsync(json);
        settings.Load();
        log.LogInformation("Database imported from {Path}", dialog.FileName);
        await PushSettingsAsync();
        await PushEverythingForActiveUserAsync();
        await ToastAsync("Backup imported. Saved job-site passwords belong to the old applicants, so enter them again.");
    }

    // ── hunting ────────────────────────────────────────────────────────────────────────────────

    private void UpdateHuntButton()
    {
        HuntButton.Content = Hunting ? "_Stop hunt" : "_Hunt now";
        // Stop is only offered once there is a running hunt to stop (not during its sign-in).
        HuntButton.IsEnabled = Hunting ? huntCts != null : ActiveUserId != null && boardSurface != null;
    }

    private async void OnHuntClicked(object sender, RoutedEventArgs e)
    {
        if (huntCts is { } running) { running.Cancel(); HuntButton.IsEnabled = false; return; }
        await StartHuntAsync();
    }

    private async Task StartHuntAsync()
    {
        if (Hunting) return;
        if (ActiveUserId is not { } userId) { await ToastAsync("Add an applicant first.", error: true); return; }
        if (boardSurface is null) { await ToastAsync($"{board.DisplayName} is still loading. Try again in a moment.", error: true); return; }
        huntBusy = true;          // taken before sign-in: that can take seconds, and nothing may start meanwhile
        UpdateHuntButton();
        try { await HuntAsync(userId, boardSurface); }
        catch (Exception ex)
        {
            log.LogError(ex, "Hunt failed");
            await ToastAsync($"The hunt stopped: {ex.Message}", error: true);
        }
        finally
        {
            huntBusy = false;
            UpdateHuntButton();
            await PostAsync(new { type = "hunt", running = false });   // the panel can never be left locked
        }
    }

    private async Task HuntAsync(int userId, IBrowserSurface surface)
    {
        var user = await profiles.LoadAsync(userId) ?? throw new InvalidOperationException("That applicant no longer exists.");
        var search = await searches.GetOrCreatePrimaryAsync(user);
        if (search.SearchTerms.Count == 0)
        {
            await ToastAsync("Add at least one search keyword on the Job Requirements tab first.", error: true);
            return;
        }
        if (board is not ISearchableBoard searchable) return;

        // Signed in first: signed-out search results hide Easy Apply and ignore half the filters.
        // A session already in the pane (however it was made) is all that's needed; only without
        // one does JobHunt try the optional saved password.
        var signIn = new SignInResult(SignInStatus.AlreadySignedIn, "");
        if (!await IsBoardSignedInAsync(surface))
        {
            var account = user.BoardAccounts.FirstOrDefault(a => a.BoardId == board.Id);
            signIn = await linkedInSignIn.EnsureSignedInAsync(surface, board.SignInUrl, account?.SignInEmail, passwords.Get(userId, board.Id), CancellationToken.None);
        }
        if (signIn.Status is not (SignInStatus.AlreadySignedIn or SignInStatus.SignedIn))
        {
            await ToastAsync(signIn.Status == SignInStatus.NoCredentials
                ? $"Sign in to {board.DisplayName} in the pane on the right (Google, Apple or email all work), then hunt again."
                : signIn.Message, error: true);
            return;
        }

        using var cts = new CancellationTokenSource();
        huntCts = cts;
        UpdateHuntButton();
        await PostAsync(new { type = "hunt", running = true, message = "Hunting…" });
        var lastPushedSeen = 0;
        var progress = new Progress<HuntProgress>(async p =>
        {
            StatusText.Text = p.Message;
            await PostAsync(new { type = "hunt", running = true, message = p.Message, p.Seen, p.New, p.Recommended });
            if (p.Seen - lastPushedSeen >= 5) { lastPushedSeen = p.Seen; await PushJobsAsync(); }
        });

        try
        {
            var pacer = new RandomPacer(() => (settings.Current.MinActionDelayMs, settings.Current.MaxActionDelayMs));
            var summary = await hunter.RunAsync(user, search, searchable, surface, pacer, progress, cts.Token);
            await PushJobsAsync();
            await PushSearchAsync();
            StatusText.Text = summary.Message;
            await PostAsync(new { type = "hunt", running = false, message = summary.Message, summary.Seen, summary.New, summary.Recommended,
                                  showBestFits = summary.Recommended > 0 });
            await ToastAsync(summary.Message, error: summary.Stop is HuntStop.Challenge or HuntStop.SignedOut or HuntStop.Unrecognized or HuntStop.Failed);
        }
        finally
        {
            if (ReferenceEquals(huntCts, cts)) huntCts = null;
            UpdateHuntButton();
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e) => huntCts?.Cancel();

    // ── command bar ────────────────────────────────────────────────────────────────────────────

    private void SetSelectedCount(int count)
    {
        selectedCount = count;
        // "_A" makes Alt+A an access key: Apply is reachable from anywhere without the mouse (2.1.1).
        ApplyButton.Content = $"_Apply ({count})";
        ApplyButton.IsEnabled = count > 0;
    }

    private void ApplyDryRunBadge() =>
        DryRunBadge.Visibility = settings.Current.DryRun ? Visibility.Visible : Visibility.Collapsed;

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        log.LogInformation("Apply clicked with {Count} selected (dry run {DryRun})", selectedCount, settings.Current.DryRun);
        MessageBox.Show(this,
            $"{selectedCount} job(s) are selected.\n\nThe Easy Apply engine is milestone 6 in docs/PLAN.md and isn't built yet — nothing was opened or sent.",
            "Apply", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Opens a tailored document in Word (or whatever handles .docx) — only files inside
    /// JobHunt's own documents folder.</summary>
    private void OpenDocument(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DocumentsRoot)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return;
        Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
    }
}

/// <summary>What a best-fit card shows.</summary>
internal static class JobCard
{
    public static object From(JobPosting j) => new
    {
        j.Id, j.Title, j.Company, j.Location, j.Url, j.Score, j.Status, j.SelectedForApply,
        workplace = j.Workplace, employmentType = j.EmploymentType,
        duration = j.ContractDurationText.Length > 0 ? j.ContractDurationText
            : j.ContractMonths is { } m ? $"{m} months" : "",
        salary = SalaryText(j),
        j.PlainDescription, j.Highlights, j.RedFlags, j.TechStack, j.Evidence,
        breakdown = j.ScoreBreakdown,
        j.ResumePath, j.CoverLetterPath,
        resumeExists = j.ResumePath.Length > 0 && File.Exists(j.ResumePath),
        coverLetterExists = j.CoverLetterPath.Length > 0 && File.Exists(j.CoverLetterPath),
        j.PostedAt, j.ApplicantCount,
    };

    private static string SalaryText(JobPosting j)
    {
        if (j.SalaryText.Length > 0) return j.SalaryText;
        if (j.SalaryMin is null && j.SalaryMax is null) return "";
        var unit = j.SalaryPeriod switch { PayPeriod.Hour => "/hr", PayPeriod.Day => "/day", PayPeriod.Week => "/wk", PayPeriod.Month => "/mo", _ => "/yr" };
        string Fmt(decimal? v) => v is null ? "?" : v >= 1000 ? $"${v / 1000:0.#}k" : $"${v:0}";
        return j.SalaryMin == j.SalaryMax || j.SalaryMin is null || j.SalaryMax is null
            ? Fmt(j.SalaryMax ?? j.SalaryMin) + unit
            : $"{Fmt(j.SalaryMin)}–{Fmt(j.SalaryMax)}{unit}";
    }
}
