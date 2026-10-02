using AutoWebNav;
using Microsoft.Extensions.Logging;

namespace JobHunt.Core.Boards;

/// <summary>Generic heuristics for spotting a bot-verification wall (CAPTCHA, WAF challenge) on a
/// page whose exact markup isn't known in advance. Shared across every board that doesn't need
/// something more specific.</summary>
public static class BoardChallengeHeuristics
{
    private const string ChallengeUrlPattern = "challenge|captcha|checkpoint|verify|blocked";

    private const string ChallengeProbeScript = """
        (function () {
          var text = (document.body && document.body.innerText || '').slice(0, 4000).toLowerCase();
          var phrase = /unusual traffic|verify you.?re a human|verify you are a human|security check|prove you.?re not a robot/.test(text);
          var iframe = !!document.querySelector(
            'iframe[src*="hcaptcha.com"], iframe[src*="recaptcha"], iframe[title*="challenge" i], ' +
            'div[class*="cf-chl"], #cf-challenge-running, iframe[src*="perimeterx"], iframe[src*="datadome"]');
          return JSON.stringify({ phrase: phrase, iframe: iframe });
        })()
        """;

    /// <summary>True when the URL's path/query looks like a challenge page, or the DOM shows a
    /// CAPTCHA/WAF widget or "prove you're human"-style text.</summary>
    public static async Task<bool> LooksLikeChallengeAsync(string url, IBrowserSurface browser, CancellationToken ct)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(url, ChallengeUrlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return true;
        try
        {
            var raw = await browser.EvalAsync(ChallengeProbeScript, ct);
            return raw.Contains("\"phrase\":true") || raw.Contains("\"iframe\":true");
        }
        catch
        {
            // A probe that can't run (page mid-navigation, etc.) is not evidence of a challenge.
            return false;
        }
    }
}

/// <summary>
/// The per-board configuration <see cref="FingerprintBoardSignIn"/> needs: where to find the
/// email field, password field and submit button, and (optionally) how to recognize "signed in"
/// from the page itself. Fingerprints should favor label text, aria attributes, placeholder and
/// visible text over exact CSS/id — those are the strategies most likely to still resolve after a
/// site redesign, and are what let <see cref="AutoWebNav.FingerprintResolver"/>'s self-heal upgrade
/// a merely-approximate first guess into a precise one on first use.
/// </summary>
public sealed class BoardSignInProfile
{
    public required string BoardName { get; init; }
    public required ElementFingerprint EmailField { get; init; }
    public required ElementFingerprint PasswordField { get; init; }
    public required ElementFingerprint SubmitButton { get; init; }
    public ElementFingerprint? ErrorMessage { get; init; }

    /// <summary>Secondary check only — callers try <see cref="IJobBoard.SessionCookieName"/>
    /// first. Optional: most boards have a session cookie worth trusting, so this can be left
    /// null and sign-in detection then falls back to "the login form is gone."</summary>
    public ElementFingerprint? SignedInIndicator { get; init; }

    /// <summary>Defaults to <see cref="BoardChallengeHeuristics.LooksLikeChallengeAsync"/>;
    /// override only if a board needs something more specific.</summary>
    public Func<string, IBrowserSurface, CancellationToken, Task<bool>>? ChallengeDetector { get; init; }

    public int ResolveTimeoutMs { get; init; } = 8_000;
    public int PollIntervalMs { get; init; } = 500;
    public int TimeoutMs { get; init; } = 25_000;
}

/// <summary>
/// A board-agnostic <see cref="IBoardSignIn"/> driven entirely by a <see cref="BoardSignInProfile"/>.
/// Locates each field with <see cref="FingerprintResolver"/> (self-healing across markup drift),
/// then acts the same way <c>LinkedInSignIn</c> does in production: a trusted click at the
/// resolved center point for the submit button, real CDP keystrokes for the email/password
/// fields — not the "set .value on the resolved element" helpers, since those are unproven
/// against a real login form's bot/validation heuristics.
/// </summary>
public sealed class FingerprintBoardSignIn(FingerprintResolver resolver, BoardSignInProfile profile, ILogger log) : IBoardSignIn
{
    public async Task<bool> IsSignedInAsync(IBrowserSurface browser, CancellationToken ct)
    {
        if (profile.SignedInIndicator is not { } indicator) return false;
        var result = await resolver.ResolveAsync(browser, indicator, highlight: false, refingerprint: true, profile.ResolveTimeoutMs, ct);
        return result.Found;
    }

    public async Task<SignInResult> EnsureSignedInAsync(IBrowserSurface browser, string signInUrl, string? email, string? password, CancellationToken ct)
    {
        if (await IsSignedInAsync(browser, ct))
            return new(SignInStatus.AlreadySignedIn, $"Already signed in to {profile.BoardName}.");
        if (await LooksLikeChallengeAsync(browser, ct)) return NeedsYou();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return new(SignInStatus.NoCredentials, $"No saved {profile.BoardName} password — sign in in the {profile.BoardName} pane.");

        var emailField = await resolver.ResolveAsync(browser, profile.EmailField, highlight: false, refingerprint: true, profile.ResolveTimeoutMs, ct);
        if (!emailField.Found)
        {
            await browser.NavigateAsync(signInUrl, ct);
            if (await IsSignedInAsync(browser, ct)) return new(SignInStatus.AlreadySignedIn, $"Already signed in to {profile.BoardName}.");
            if (await LooksLikeChallengeAsync(browser, ct)) return NeedsYou();
            emailField = await resolver.ResolveAsync(browser, profile.EmailField, highlight: false, refingerprint: true, profile.ResolveTimeoutMs, ct);
            if (!emailField.Found) return new(SignInStatus.Failed, $"{profile.BoardName}'s sign-in form didn't appear.");
        }

        log.LogInformation("Signing in to {Board} as {Email}", profile.BoardName, email);
        await BrowserActions.TypeViaKeystrokesAsync(browser, emailField.CenterX, emailField.CenterY, email, ct);

        var passwordField = await resolver.ResolveAsync(browser, profile.PasswordField, highlight: false, refingerprint: true, profile.ResolveTimeoutMs, ct);
        if (!passwordField.Found) return new(SignInStatus.Failed, $"{profile.BoardName}'s password field didn't appear.");
        await BrowserActions.TypeViaKeystrokesAsync(browser, passwordField.CenterX, passwordField.CenterY, password, ct);

        var submit = await resolver.ResolveAsync(browser, profile.SubmitButton, highlight: false, refingerprint: true, profile.ResolveTimeoutMs, ct);
        if (!submit.Found) return new(SignInStatus.Failed, $"{profile.BoardName}'s sign-in button wasn't found.");
        await browser.ClickAtPointAsync(submit.CenterX, submit.CenterY, ct);

        var waited = 0;
        while (waited < profile.TimeoutMs)
        {
            await Task.Delay(profile.PollIntervalMs, ct);
            waited += profile.PollIntervalMs;

            // With a SignedInIndicator, trust it. Without one (most boards, since the caller
            // already checks IJobBoard.SessionCookieName before ever reaching here — see
            // MainWindow.IsBoardSignedInAsync), the best signal this board-agnostic engine has of
            // its own is that the login form it just filled in is no longer on the page.
            var signedIn = profile.SignedInIndicator != null
                ? await IsSignedInAsync(browser, ct)
                : !(await resolver.ResolveAsync(browser, profile.EmailField, highlight: false, refingerprint: false, 1_000, ct)).Found;
            if (signedIn)
            {
                log.LogInformation("Signed in to {Board}", profile.BoardName);
                return new(SignInStatus.SignedIn, $"Signed in to {profile.BoardName}.");
            }
            if (await LooksLikeChallengeAsync(browser, ct)) return NeedsYou();

            if (profile.ErrorMessage is { } errorFp)
            {
                var error = await resolver.ResolveAsync(browser, errorFp, highlight: false, refingerprint: false, 1_000, ct);
                if (error.Found)
                {
                    var message = string.IsNullOrWhiteSpace(error.Text) ? "the sign-in was rejected." : error.Text;
                    log.LogWarning("{Board} rejected the sign-in: {Error}", profile.BoardName, message);
                    return new(SignInStatus.Failed, $"{profile.BoardName} rejected the sign-in: {message}");
                }
            }
        }
        return new(SignInStatus.Failed, $"{profile.BoardName} didn't finish signing in. Check the {profile.BoardName} pane.");
    }

    private Task<bool> LooksLikeChallengeAsync(IBrowserSurface browser, CancellationToken ct) =>
        (profile.ChallengeDetector ?? BoardChallengeHeuristics.LooksLikeChallengeAsync)(browser.CurrentUrl, browser, ct);

    private SignInResult NeedsYou()
    {
        log.LogWarning("{Board} is asking for a verification step; handing over to the user", profile.BoardName);
        return new(SignInStatus.NeedsYou, $"{profile.BoardName} wants to verify it's you. Finish that in the {profile.BoardName} pane. JobHunt won't answer it for you.");
    }
}
