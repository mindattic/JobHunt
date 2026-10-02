using AutoWebNav;

namespace JobHunt.Core.Boards.Indeed;

/// <summary>
/// Builds the <see cref="BoardSignInProfile"/> <see cref="FingerprintBoardSignIn"/> uses to sign
/// in to Indeed. Fingerprints favor name/type/aria/placeholder over exact CSS, since those are
/// the strategies <see cref="FingerprintResolver"/>'s cascade is most likely to still resolve
/// after a redesign.
/// <para>
/// The signed-in indicator is confirmed against a live pane; the email/password/submit
/// fingerprints are NOT yet (the pane was already signed in, so the form never showed — confirm
/// them with Spectator Mode the next time a sign-in is needed). One specific risk: Indeed's sign-in has
/// historically been a two-step flow (email, then a "Continue" click, then password on a second
/// screen) rather than both fields on one page. This profile assumes both fields are reachable
/// without an intermediate click; if that's wrong, <see cref="FingerprintBoardSignIn"/> will
/// report <see cref="SignInStatus.Failed"/> at the password-field step and this profile needs a
/// two-step variant.
/// </para>
/// </summary>
public static class IndeedSignInProfile
{
    public static BoardSignInProfile Build() => new()
    {
        BoardName = "Indeed",
        EmailField = new ElementFingerprint
        {
            Tag = "input",
            TypeAttr = "email",
            NameAttr = "__email",
            AriaLabel = "email address",
            Placeholder = "Email address",
            NearbyLabelText = "Email address",
        },
        PasswordField = new ElementFingerprint
        {
            Tag = "input",
            TypeAttr = "password",
            NameAttr = "__password",
            AriaLabel = "password",
            Placeholder = "Password",
            NearbyLabelText = "Password",
        },
        SubmitButton = new ElementFingerprint
        {
            Tag = "button",
            TypeAttr = "submit",
            VisibleText = "Sign in",
            AriaLabel = "Sign in",
        },
        ErrorMessage = new ElementFingerprint
        {
            AriaRole = "alert",
        },
        // Confirmed against a live signed-in pane (2026-09-30): Indeed's global nav only renders
        // the account-menu button for a signed-in visitor.
        SignedInIndicator = new ElementFingerprint
        {
            Tag = "button",
            CssSelector = "button[data-gnav-element-name=\"AccountMenu\"]",
            AriaLabel = "Account",
        },
    };
}
