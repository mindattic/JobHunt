using AutoWebNav;

namespace JobHunt.Core.Boards;

public enum SignInStatus
{
    AlreadySignedIn,
    SignedIn,
    /// <summary>No password saved (or auto sign-in is off) — the user signs in in the pane.</summary>
    NoCredentials,
    /// <summary>The board asked for a verification code, CAPTCHA or security check. Only a person
    /// may answer those; the pane is left on that page for the user.</summary>
    NeedsYou,
    /// <summary>The board rejected the email/password, or the page never settled.</summary>
    Failed,
}

public sealed record SignInResult(SignInStatus Status, string Message);

/// <summary>
/// Signs a user in to one job board with their saved account when the pane is signed out. Every
/// implementation types into the real sign-in form with trusted input and clicks Sign in —
/// nothing else. Any challenge (verification code, CAPTCHA, "let's do a quick security check")
/// stops it immediately: those are for the account holder, and automating past them is exactly
/// what gets accounts restricted. The password is never logged.
/// </summary>
public interface IBoardSignIn
{
    Task<SignInResult> EnsureSignedInAsync(IBrowserSurface browser, string signInUrl,
        string? email, string? password, CancellationToken ct);

    /// <summary>Whether the page currently shows a signed-in board — however the user signed in
    /// (Google, Apple, email). Reads the page only; never navigates or types.</summary>
    Task<bool> IsSignedInAsync(IBrowserSurface browser, CancellationToken ct);
}
