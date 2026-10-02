using System.Text.Json;
using AutoWebNav;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Boards.LinkedIn;

using JobHunt.Core.Boards;

/// <summary>
/// Signs in to LinkedIn with the saved account when the pane is signed out. It types into the real
/// sign-in form with trusted input and clicks Sign in — nothing else. Any challenge (verification
/// code, CAPTCHA, "let's do a quick security check") stops it immediately: those are for the
/// account holder, and automating past them is exactly what gets accounts restricted.
/// The password is never logged.
/// </summary>
public sealed class LinkedInSignIn(ILogger<LinkedInSignIn> log) : IBoardSignIn
{
    /// <summary>Delay between page checks while waiting for the sign-in to land. Lowered in tests.</summary>
    public int PollIntervalMs { get; init; } = 500;
    public int TimeoutMs { get; init; } = 25_000;

    internal const string ProbeScript = """
        (function () {
          var url = location.href;
          // The PATH only: a signed-out search for "verification engineer" is not a challenge.
          var challenge = /\/checkpoint\/|\/challenge|\/uas\/consumer-captcha|two-step|verification/i.test(location.pathname)
            || !!document.querySelector('iframe[src*="captcha"], #captcha-internal, input[name="pin"], #input__phone_verification_pin');
          var user = document.querySelector('#username, input[name="session_key"]');
          var pass = document.querySelector('#password, input[name="session_password"]');
          // The 2026 nav has hashed class names; its stable hooks are data-view-name. "Me" and
          // Messaging render only for a signed-in member (read live 2026-09-30). The older
          // global-nav selectors stay as fallbacks for the previous layout.
          var signedIn = !!document.querySelector('[data-view-name="navigation-settings"], [data-view-name="navigation-messaging"], ' +
            '#global-nav .global-nav__me, .global-nav__me-photo, [data-control-name="nav.settings"], ' +
            'header.global-nav, nav.global-nav, .global-nav__primary-link-me-menu-trigger, img.global-nav__me-photo, a[href*="/mynetwork/"][class*="global-nav"]');
          var errorEl = document.querySelector('#error-for-password, #error-for-username, .form__label--error, .alert-content');
          return JSON.stringify({
            url: url, challenge: challenge, signedIn: signedIn, loginForm: !!(user && pass),
            error: errorEl && errorEl.offsetParent !== null ? (errorEl.textContent || '').trim().slice(0, 200) : ''
          });
        })()
        """;

    private sealed record Probe(string Url, bool Challenge, bool SignedIn, bool LoginForm, string Error);

    public async Task<SignInResult> EnsureSignedInAsync(IBrowserSurface browser, string signInUrl, string? email, string? password, CancellationToken ct)
    {
        var page = await ProbeAsync(browser, ct);
        if (page.SignedIn) return new(SignInStatus.AlreadySignedIn, "Already signed in to LinkedIn.");
        if (page.Challenge) return NeedsYou();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return new(SignInStatus.NoCredentials, "No saved LinkedIn password — sign in in the LinkedIn pane.");

        if (!page.LoginForm)
        {
            await browser.NavigateAsync(signInUrl, ct);
            page = await ProbeAsync(browser, ct);
            if (page.SignedIn) return new(SignInStatus.AlreadySignedIn, "Already signed in to LinkedIn.");
            if (page.Challenge) return NeedsYou();
            if (!page.LoginForm) return new(SignInStatus.Failed, "LinkedIn's sign-in form didn't appear.");
        }

        log.LogInformation("Signing in to LinkedIn as {Email}", email);
        await FillAsync(browser, "#username, input[name=\"session_key\"]", email, ct);
        await FillAsync(browser, "#password, input[name=\"session_password\"]", password, ct);
        await ClickAsync(browser, "button[type=submit][aria-label*=\"Sign in\" i], form[action*=\"login\"] button[type=submit], button[data-litms-control-urn*=\"login-submit\"]", ct);

        var waited = 0;
        while (waited < TimeoutMs)
        {
            await Task.Delay(PollIntervalMs, ct);
            waited += PollIntervalMs;
            page = await ProbeAsync(browser, ct);
            if (page.SignedIn)
            {
                log.LogInformation("Signed in to LinkedIn");
                return new(SignInStatus.SignedIn, "Signed in to LinkedIn.");
            }
            if (page.Challenge) return NeedsYou();
            if (page.LoginForm && page.Error.Length > 0)
            {
                log.LogWarning("LinkedIn rejected the sign-in: {Error}", page.Error);
                return new(SignInStatus.Failed, $"LinkedIn rejected the sign-in: {page.Error}");
            }
        }
        return new(SignInStatus.Failed, "LinkedIn didn't finish signing in. Check the LinkedIn pane.");
    }

    /// <summary>Whether the page currently shows a signed-in LinkedIn — however the user signed in
    /// (Google, Apple, email). Reads the page only; never navigates or types.</summary>
    public async Task<bool> IsSignedInAsync(IBrowserSurface browser, CancellationToken ct) =>
        (await ProbeAsync(browser, ct)).SignedIn;

    private SignInResult NeedsYou()
    {
        log.LogWarning("LinkedIn is asking for a verification step; handing over to the user");
        return new(SignInStatus.NeedsYou, "LinkedIn wants to verify it's you. Finish that in the LinkedIn pane. JobHunt won't answer it for you.");
    }

    private static async Task<Probe> ProbeAsync(IBrowserSurface browser, CancellationToken ct)
    {
        var raw = await browser.EvalAsync(ProbeScript, ct);
        return JsonSerializer.Deserialize<Probe>(raw, JsonOptions)
            ?? new Probe("", false, false, false, "");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Focuses the field, clears it, and types with trusted keystrokes — LinkedIn's form
    /// is React-controlled and ignores values set from script.</summary>
    private static async Task FillAsync(IBrowserSurface browser, string selector, string text, CancellationToken ct)
    {
        var focused = await browser.EvalAsync($$"""
            (function () {
              var el = document.querySelector({{JsonSerializer.Serialize(selector)}});
              if (!el) return JSON.stringify({ ok: false });
              el.focus(); el.select && el.select();
              // Type only if the field really has focus — never into whatever else does.
              return JSON.stringify({ ok: document.activeElement === el });
            })()
            """, ct);
        if (!focused.Contains("\"ok\":true")) throw new InvalidOperationException($"Sign-in field not found ({selector}).");
        await browser.TypeTextAsync(text, ct);
    }

    private static async Task ClickAsync(IBrowserSurface browser, string selector, CancellationToken ct)
    {
        var raw = await browser.EvalAsync($$"""
            (function () {
              var el = document.querySelector({{JsonSerializer.Serialize(selector)}});
              if (!el) return JSON.stringify({ found: false });
              el.scrollIntoView({ block: 'center' });
              var r = el.getBoundingClientRect();
              return JSON.stringify({ found: true, x: r.left + r.width / 2, y: r.top + r.height / 2 });
            })()
            """, ct);
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.GetProperty("found").GetBoolean()) throw new InvalidOperationException("LinkedIn's Sign in button wasn't found.");
        await browser.ClickAtPointAsync(doc.RootElement.GetProperty("x").GetDouble(), doc.RootElement.GetProperty("y").GetDouble(), ct);
    }
}
