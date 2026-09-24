# JobHunt

Fill in your information once. JobHunt then finds jobs that match you, writes a résumé and cover
letter for each one, and fills in the application forms.

JobHunt is a Windows desktop app (WPF + WebView2). The left panel holds your profile,
recommendations, applications and settings. The right pane is LinkedIn itself, signed in as you. At
the bottom is the big blue **Apply (N)** button.

> **Status:** the foundation is done: data model, database, scoring, search URLs, settings,
> bring-your-own-key and the app shell. Searching, document generation and applying are the
> next milestones. See [docs/PLAN.md](docs/PLAN.md).

## First run

1. **Settings → Your AI provider.** Pick Claude, OpenAI, Gemini or Kimi and paste your own API key.
   It is stored in MindAttic Vault on this computer, never in the database.
2. **Profile.** Enter your details, or import a `.jobhunt-profile.json` file.
3. **Sign in to LinkedIn** in the right-hand pane. The session stays in JobHunt's own browser
   profile, and your password is never stored.

**Dry run is on by default.** Apply goes through every step of each application and stops just
before the final Submit, and each rehearsal is logged. Turn it off in Settings only once you trust
what it does.

## Where things live

| What | Where |
|---|---|
| Database (profile, jobs, applications, settings) | `%LocalAppData%\MindAttic\JobHunt\jobhunt.db` |
| Logs | `%LocalAppData%\MindAttic\JobHunt\Logs\jobhunt-<date>.log` |
| Tailored documents | `Documents\JobHunt\Applications\<Company - Title (linkedin-id)>\First-Last-Résumé.docx` |
| LinkedIn sign-in | `%LocalAppData%\MindAttic\JobHunt\WebView2\` |

**Import and export (JSON):**
- Profile tab: the profile alone, as `.jobhunt-profile.json`.
- Settings → Backup & restore: everything JobHunt holds, as `.jobhunt-backup.json`. That's your profile, searches, jobs, and applications with their outcome history. Importing a backup replaces the database's contents in one transaction.
- API keys are never exported.

The environment variables `JOBHUNT_DATA_DIR` and `JOBHUNT_DOCUMENTS_DIR` override these locations
(used for testing, or a portable install).

## Accessibility

JobHunt targets **WCAG 2.2 AA**. `tools/verify-ui.mjs` proves it against the real app: it starts
JobHunt on scratch data, fills every section, and checks all five tabs in both themes.

- **axe-core:** every wcag2a/2aa/21a/21aa/22aa rule, and any violation fails.
- **Focus (2.4.7, 2.4.11):** a Tab walk over every control. Each must show a visible focus ring
  and must not be hidden behind the sticky header or save bar.
- **Layout (1.4.10, 1.4.12):** reflow at 320 CSS px, and the text-spacing overrides.
- **Forms (3.3.1, 3.3.7):** error identification and redundant entry.
- **Native WPF bar:** its colour contrast, checked arithmetically.

```
cd tools && npm install && node verify-ui.mjs
```

**Design choices behind it:**
- Every control is at least 24×24 px.
- Form-field borders are 3:1 or better.
- Errors are announced via `role=alert` and stay until dismissed; confirmations are polite.
- Help text is tied to its field with `aria-describedby`.
- Checkbox groups are named, and best-fit titles are real buttons.
- The WPF status line is a live region, and **Alt+A** triggers Apply.

**Not machine-checked:** screen-reader walkthroughs (NVDA/Narrator), and Tab moving between the panel, the LinkedIn pane and the WPF bar. Check these by hand before a release.

## For developers

```
dotnet build JobHunt.slnx
dotnet test JobHunt.Tests
dotnet run --project JobHunt.App

cd tools && npm install
node verify-linkedin.mjs   # LinkedIn page scripts vs. modelled markup (jsdom)
node verify-hunt.mjs       # a whole hunt through the real app, against a local LinkedIn stand-in
node verify-ui.mjs         # WCAG 2.2 AA
```

**When LinkedIn changes its markup:** save the new page into `tools/fixtures/linkedin/`, add
checks for it to `verify-linkedin.mjs`, then fix `JobHunt.Core/Boards/LinkedIn/Scripts/*.js`.
Every field there already tries several selectors in turn.

| Project | Role |
|---|---|
| `JobHunt.Core` | Models, EF Core `JobHuntDb` and migrations, `ProfileStore` / `JobStore` / `SettingsStore`, `IJobBoard` + `LinkedInJobBoard`, `FitScorer`, `DocumentNaming`, BYOK (`ByokKeys`) and `IJobHuntLlm` over Legion. |
| `JobHunt.App` | WPF host: the panel (`wwwroot/panel.*`) and the board pane, bridged over postMessage, plus the native command bar. |
| `JobHunt.Tests` | NUnit tests against real in-memory SQLite, using the app's own migrations. |

Browser automation comes from the shared [AutoWebNav](https://github.com/mindattic/AutoWebNav)
packages. Change browser mechanics there, not here.

**Schema changes:** add a new migration with `dotnet ef migrations add <Name> --project JobHunt.Core --output-dir Data/Migrations`.
Never delete or regenerate existing migrations. Real databases already record them, and a rewritten
history is the "table already exists" startup crash. If a database *does* carry a migration this
build doesn't know, `DatabaseMigrator` moves it aside as `jobhunt.unrecognized-<stamp>.db` (it never
deletes it), starts a fresh one, and tells the user.
