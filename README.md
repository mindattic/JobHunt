# JobHunt

Fill in your job-application details once: JobHunt searches LinkedIn for you and scores every job against your profile. Tailored résumés and one-click applying are on the roadmap.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![WPF + WebView2](https://img.shields.io/badge/WPF-WebView2-0078D6) ![SQLite EF Core](https://img.shields.io/badge/SQLite-EF%20Core-003B57) ![WCAG 2.2 AA](https://img.shields.io/badge/WCAG-2.2%20AA-2f7a4f) ![Status in development](https://img.shields.io/badge/Status-in%20development-d9762b)

![JobHunt app icon: a magnifying glass picking one candidate out of a line-up](assets/jobhunt.png)

JobHunt is a Windows desktop app (WPF + WebView2). The left panel holds your profile, job requirements, recommendations, applications and settings. The right pane is the job board itself, signed in as you. At the bottom is the big blue **Apply (N)** button.

```text
 ┌──────────────────────────────── JobHunt ────────────────────────────────────┐
 │ Applicant ▾  Add Edit Delete         │                                      │
 │ Applicant | Job Requirements |       │                                      │
 │ Best Fits | Applications | Settings  │     LinkedIn (or Indeed), signed in  │
 │                                      │     as you, in JobHunt's own browser │
 │  panel: your profile, the searches   │     profile                          │
 │  it runs, ranked jobs with a fit     │                                      │
 │  score you can inspect               │                                      │
 ├──────────────────────────────────────┴──────────────────────────────────────┤
 │  DRY RUN                                               [ Apply (N) ]  Alt+A │
 └─────────────────────────────────────────────────────────────────────────────┘
```

Status: the foundation, the applicant and requirements UI, and most of the search engine are done and tested. Reading each job with an LLM, generating documents and applying are the next milestones. See [docs/PLAN.md](docs/PLAN.md) for the milestone list and what is proven.

## Why

- Type your work history, skills, preferences and screening answers once, not once per application.
- Let JobHunt walk LinkedIn search results at a human pace while you do something else.
- See why a job ranks where it does: a 0 to 100 fit score with a per-criterion breakdown and the reason behind every filter.
- Keep several applicants (for example you and a partner) apart, each with their own board sign-in.
- Rehearse safely: dry run is on by default and stops before the final Submit.
- Own your data: one local SQLite file, JSON import and export, and API keys that never leave this computer's vault.

## Features

Built and tested today:

- **Applicant profile** covering what applications ask for: contact details, work authorization, compensation, work preferences, work history, education, skills, certifications, languages, projects, accomplishments, references, voluntary EEO disclosures (default "decline"), keyword lists, text blocks, custom fields and a screening-question answer bank. Import and export as `.jobhunt-profile.json`.
- **Multiple applicants** with an applicant dropdown and Add, Edit and Delete (delete hides; restore from Settings). Each applicant has their own LinkedIn session; the email is kept in the profile and the password is DPAPI-encrypted in MindAttic Vault, and auto sign-in stops at any verification challenge.
- **Job requirements** tab with a live preview of the LinkedIn searches it will run, and a "Save and show on LinkedIn" button.
- **Hunting:** `HuntRunner` runs every search term page by page at a human pace, clicks each result card, reads the details pane, de-duplicates across terms, never touches jobs you applied to or dismissed, re-reads details only after 3 days, stops at once on any challenge or sign-out, gives each job a provisional listing-level score, and fills Best Fits.
- **Fit scorer** with hard pass/fail filters and adjustable weights (see Fit score below).
- **Boards behind one interface:** each site is an `IJobBoard`. LinkedIn is the primary board; an Indeed board is registered as a pilot; its page scripts are checked against captured Indeed pages.
- **Bring your own key** for Claude, OpenAI, Gemini or Kimi. The chosen provider runs first and the others are fallbacks.
- **Whole-database backup and restore** as `.jobhunt-backup.json`.
- **Accessibility:** targets WCAG 2.2 AA and is checked against the real app (see Accessibility below).

On the roadmap (see [docs/PLAN.md](docs/PLAN.md)):

- One LLM call per job returning a plain-English description, employment type, contract terms, salary and requirements matched to profile facts with evidence.
- A `First-Last-Résumé.docx` and `First-Last-Cover-Letter.docx` written for each job, following [the résumé guide](docs/RESUME_GUIDE.md), with a validator that rejects invented facts.
- **Apply (N):** a pre-flight question scan and an Easy Apply state machine that stops before Submit in dry run, with a daily cap.
- Tracking application status back from LinkedIn, scheduled daily hunts and CSV export.

## Quick start

Prerequisites: Windows, the .NET 10 SDK, and the Microsoft Edge WebView2 runtime.

```powershell
git clone https://github.com/mindattic/JobHunt.git
cd JobHunt
.\launch.bat
```

`launch.bat` runs `tools\deploy.ps1 -Launch`: it stops any running JobHunt, clears the build cache, publishes a Release build to `C:\Apps\JobHunt\` and starts that copy. Your data under `%LocalAppData%\MindAttic\JobHunt\` and your documents are never touched by a redeploy.

### First run

1. **Settings, Your AI provider.** Pick Claude, OpenAI, Gemini or Kimi and paste your own API key. It is stored in MindAttic Vault on this computer, never in the database.
2. **Applicant.** Enter your details, or import a `.jobhunt-profile.json` file.
3. **Sign in to LinkedIn** in the right-hand pane. The session stays in JobHunt's own browser profile, and your password is never stored by the board pane.

**Dry run is on by default.** Apply goes through every step of each application and stops just before the final Submit, and each rehearsal is logged. Turn it off in Settings only once you trust what it does.

## Fit score

Stage 1 is hard filters, each pass or fail with a stated reason. Stage 2 is a weighted score from 0 to 100 with these default weights, all adjustable:

| Criterion | Weight |
|---|---|
| Required skills | 35 |
| Nice-to-have skills | 10 |
| Title match | 15 |
| Salary | 15 |
| Workplace | 10 |
| Employment type | 10 |
| Freshness | 5 |

The LLM supplies facts and code does the arithmetic. A field the posting leaves unstated never fails a filter; it earns half credit instead.

## Where things live

| What | Where |
|---|---|
| Database (profile, jobs, applications, settings) | `%LocalAppData%\MindAttic\JobHunt\jobhunt.db` |
| Logs | `%LocalAppData%\MindAttic\JobHunt\Logs\jobhunt-<date>.log` |
| Tailored documents | `Documents\JobHunt\Applications\<Company - Title (linkedin-id)>\First-Last-Résumé.docx` |
| LinkedIn sign-in | `%LocalAppData%\MindAttic\JobHunt\WebView2\` |

Import and export (JSON):

- Applicant tab: one applicant's profile, as `.jobhunt-profile.json`.
- Settings, Backup and restore: everything JobHunt holds, as `.jobhunt-backup.json`. That is your profile, searches, jobs, and applications with their outcome history. Importing a backup replaces the database's contents in one transaction.
- API keys are never exported.

The environment variables `JOBHUNT_DATA_DIR` and `JOBHUNT_DOCUMENTS_DIR` override these locations (used for testing, or a portable install).

## Accessibility

JobHunt targets **WCAG 2.2 AA**. `tools/verify-ui.mjs` proves it against the real app: it starts JobHunt on scratch data, fills every section, and checks all five tabs in both themes.

- **axe-core:** every wcag2a, wcag2aa, wcag21a, wcag21aa and wcag22aa rule, and any violation fails.
- **Focus (2.4.7, 2.4.11):** a Tab walk over every control. Each must show a visible focus ring and must not be hidden behind the sticky header or save bar.
- **Layout (1.4.10, 1.4.12):** reflow at 320 CSS px, and the text-spacing overrides.
- **Forms (3.3.1, 3.3.7):** error identification and redundant entry.
- **Native WPF bar:** its colour contrast, checked arithmetically.

Run it:

```powershell
cd tools
npm install
node verify-ui.mjs
```

Design choices behind it:

- Every control is at least 24 by 24 px.
- Form-field borders are 3:1 or better.
- Errors are announced via `role=alert` and stay until dismissed; confirmations are polite.
- Help text is tied to its field with `aria-describedby`.
- Checkbox groups are named, and best-fit titles are real buttons.
- The WPF status line is a live region, and **Alt+A** triggers Apply.

Not machine-checked: screen-reader walkthroughs (NVDA, Narrator), and Tab moving between the panel, the LinkedIn pane and the WPF bar. Check these by hand before a release.

## Building and testing

```powershell
dotnet build JobHunt.slnx
dotnet test JobHunt.Tests
dotnet run --project JobHunt.App
```

Browser-side checks live in `tools/` (Node):

```powershell
cd tools
npm install
node verify-linkedin.mjs
node verify-indeed.mjs
node verify-hunt.mjs
node verify-ui.mjs
```

| Script | What it proves |
|---|---|
| `verify-linkedin.mjs` | LinkedIn page scripts against modelled markup (jsdom) |
| `verify-indeed.mjs` | Indeed page scripts against captured Indeed markup (jsdom) |
| `verify-hunt.mjs` | A whole hunt through the real app, against a local LinkedIn stand-in |
| `verify-ui.mjs` | WCAG 2.2 AA |

**When LinkedIn changes its markup:** save the new page into `tools/fixtures/linkedin/`, add checks for it to `verify-linkedin.mjs`, then fix `JobHunt.Core/Boards/LinkedIn/Scripts/*.js`. Every field there already tries several selectors in turn.

**Schema changes:** add a new migration with:

```powershell
dotnet ef migrations add <Name> --project JobHunt.Core --output-dir Data/Migrations
```

Never delete or regenerate existing migrations. Real databases already record them, and a rewritten history is the "table already exists" startup crash. If a database does carry a migration this build does not know, `DatabaseMigrator` moves it aside as `jobhunt.unrecognized-<stamp>.db` (it never deletes it), starts a fresh one, and tells the user.

## Project layout

| Project | Role |
|---|---|
| `JobHunt.Core` | Models, EF Core `JobHuntDb` and migrations, `ProfileStore`, `JobStore`, `SettingsStore`, `IJobBoard` with `LinkedInJobBoard` and `IndeedJobBoard`, `HuntRunner`, `FitScorer`, `DocumentNaming`, BYOK (`ByokKeys`) and `IJobHuntLlm` over MindAttic.Legion. |
| `JobHunt.App` | WPF host: the panel (`wwwroot/panel.*`) and the board pane, bridged over postMessage, plus the native command bar. |
| `JobHunt.Tests` | NUnit tests against real in-memory SQLite, using the app's own migrations. |
| `tools` | Deploy script, page-script and end-to-end verifiers, and saved board fixtures. |

Browser automation comes from the shared [AutoWebNav](https://github.com/mindattic/AutoWebNav) packages. Change browser mechanics there, not here. JobHunt is standalone and never takes a project reference on Automata.

## Documentation

- [docs/PLAN.md](docs/PLAN.md): decisions, what is stored, the fit score and the milestone status.
- [The résumé guide](docs/RESUME_GUIDE.md): the rules for generated résumés and cover letters, researched from 37 sources.
- [AGENTS.md](AGENTS.md): entry point for AI agents working in this repo.

## License

This repository has no LICENSE file; all rights are reserved.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [AutoWebNav](https://github.com/mindattic/AutoWebNav), the browser-automation library JobHunt is built on.
