using System.Text.Json;
using AutoWebNav;
using JobHunt.Core.Boards;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobHunt.Tests;

[TestFixture]
public class FingerprintBoardSignInTests
{
    private static BoardSignInProfile Profile(ElementFingerprint? signedInIndicator = null, ElementFingerprint? errorMessage = null) => new()
    {
        BoardName = "TestBoard",
        EmailField = new ElementFingerprint { Tag = "input", NameAttr = "email" },
        PasswordField = new ElementFingerprint { Tag = "input", NameAttr = "password" },
        SubmitButton = new ElementFingerprint { Tag = "button", VisibleText = "Sign in" },
        SignedInIndicator = signedInIndicator,
        ErrorMessage = errorMessage,
        ResolveTimeoutMs = 200,
        PollIntervalMs = 1,
        TimeoutMs = 50,
    };

    private static FingerprintBoardSignIn SignIn(BoardSignInProfile profile) =>
        new(new FingerprintResolver(NullLogger<FingerprintResolver>.Instance) { PollIntervalMs = 1 }, profile, NullLogger<FingerprintBoardSignIn>.Instance);

    private static string Found(string? text = null) =>
        $$"""{"found":true,"unique":true,"strategy":"css","score":10,"ambiguous":false,"candidateCount":1,"centerX":10,"centerY":10,"text":{{(text is null ? "null" : JsonSerializer.Serialize(text))}},"refreshedFingerprint":null}""";

    private static string NotFound() =>
        """{"found":false,"unique":false,"strategy":null,"score":0,"ambiguous":false,"candidateCount":0,"centerX":0,"centerY":0,"text":null,"refreshedFingerprint":null}""";

    [Test]
    public async Task AlreadySignedIn_WhenIndicatorResolves_DoesNothing()
    {
        var browser = new FakeBrowser([Found()]);
        var result = await SignIn(Profile(signedInIndicator: new ElementFingerprint { Id = "avatar" }))
            .EnsureSignedInAsync(browser, "https://example.test/login", "a@b.c", "pw", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.AlreadySignedIn));
        Assert.That(browser.Typed, Is.Empty);
    }

    [Test]
    public async Task NoPassword_LeavesSignInToTheUser()
    {
        var browser = new FakeBrowser([]);
        var result = await SignIn(Profile()).EnsureSignedInAsync(browser, "https://example.test/login", "a@b.c", null, default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.NoCredentials));
        Assert.That(browser.Typed, Is.Empty);
    }

    [Test]
    public async Task ChallengeUrl_StopsAndHandsOver_BeforeTypingAnything()
    {
        var browser = new FakeBrowser([]) { CurrentUrl = "https://example.test/challenge" };
        var result = await SignIn(Profile()).EnsureSignedInAsync(browser, "https://example.test/login", "a@b.c", "pw", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.NeedsYou));
        Assert.That(browser.Typed, Is.Empty);
    }

    [Test]
    public async Task LoginForm_IsFilledWithTrustedTyping_AndSubmitted_WithNoSignedInIndicator()
    {
        // No SignedInIndicator (the common case — see BoardSignInProfile's doc comment): success
        // after submit is read as "the email field is no longer on the page."
        var browser = new FakeBrowser([Found(), Found(), Found(), NotFound()]);
        var result = await SignIn(Profile()).EnsureSignedInAsync(browser, "https://example.test/login", "renee@example.com", "s3cret", default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SignInStatus.SignedIn));
            Assert.That(browser.Typed, Is.EqualTo(new[] { "renee@example.com", "s3cret" }));
            Assert.That(browser.Clicks, Is.EqualTo(3), "email field, password field, then submit");
        });
    }

    [Test]
    public async Task WrongPassword_IsReported()
    {
        var browser = new FakeBrowser([Found(), Found(), Found(), Found(), Found("Wrong email or password.")]);
        var result = await SignIn(Profile(errorMessage: new ElementFingerprint { AriaRole = "alert" }))
            .EnsureSignedInAsync(browser, "https://example.test/login", "a@b.c", "bad", default);
        Assert.That(result.Status, Is.EqualTo(SignInStatus.Failed));
        Assert.That(result.Message, Does.Contain("Wrong email or password"));
    }

    /// <summary>Answers a FingerprintResolver resolve script with a scripted sequence of JSON
    /// envelopes (the last one repeats); answers every other script (BrowserActions' select-all /
    /// readback helpers, the challenge probe) with a generic success envelope.</summary>
    private sealed class FakeBrowser(params string[] resolves) : IBrowserSurface
    {
        private int next;
        public List<string> Typed { get; } = [];
        public int Clicks { get; private set; }
        public string CurrentUrl { get; set; } = "https://example.test/login";

        public Task<string> EvalAsync(string script, CancellationToken ct) => Task.FromResult(
            script.Contains("__automataResolve(")
                ? resolves.Length == 0 ? NotFound() : resolves[Math.Min(next++, resolves.Length - 1)]
                : """{"ok":true,"value":null,"phrase":false,"iframe":false}""");

        public Task TypeTextAsync(string text, CancellationToken ct) { Typed.Add(text); return Task.CompletedTask; }
        public Task ClickAtPointAsync(double x, double y, CancellationToken ct) { Clicks++; return Task.CompletedTask; }
        public Task NavigateAsync(string url, CancellationToken ct) { CurrentUrl = url; return Task.CompletedTask; }
        public Task InjectFileAsync(string filePath, string elementJs, CancellationToken ct) => Task.CompletedTask;
        public Task PressEnterAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<double> SetZoomAsync(double factor, CancellationToken ct) => Task.FromResult(1.0);
    }
}
