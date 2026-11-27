---
codex: 1
project: JobHunt
code: JH
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# JobHunt — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| JH-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| JH-T2 | 2026-10-13 | chore | closed | Move deploy script to repo-root deploy.ps1 (-Force/-Launch, rebuild only on change) and generate C:\Apps\JobHunt\deploy.bat in place of launch.bat | deploy.ps1 |
| JH-T3 | 2026-10-13 | feature | wont-do | Move to MindAttic.Components.Wpf 5.0.0 and turn on McWindow.DarkTitleBar for its windows | portfolio reset: stale: app is on MindAttic.Components.Wpf 19.0.0 |
| JH-T4 | 2026-10-14 | chore | open | NUnit tests for LinkedInSearcher browser flow (JobHunt.Core/Boards/LinkedIn/LinkedInSearcher.cs) with a fake IBrowserSurface + NoPacer: OpenResultsAsync maps challenge/signedOut/noResults states, returns Unrecognized after ResultsTimeout, scrolls until AtBottom or MaxScrollSteps, drops blank-id cards and undoubles text; OpenListingAsync clicks the card target else navigates to ViewUrl, ignores a pane showing a different job id, returns null on DetailsTimeout, throws BoardChallengeException/BoardSignedOutException; embedded scripts load - these guard the live LinkedIn scrape | — |
| JH-T5 | 2026-10-14 | chore | wont-do | NUnit tests for IndeedSearcher browser flow (JobHunt.Core/Boards/Indeed/IndeedSearcher.cs) with a fake IBrowserSurface + NoPacer: OpenResultsAsync state mapping (challenge/signedOut/noResults/Unrecognized on timeout), scroll loop bounds, card filtering; OpenListingAsync card-click vs /viewjob?jk= fallback, stale-pane id mismatch, null on DetailsTimeout, BoardChallengeException/BoardSignedOutException; Indeed embedded scripts load - only Map/ParseWorkplace are covered today | portfolio reset: Indeed is a pilot board, out of the milestone |
| JH-T6 | 2026-10-14 | chore | wont-do | NUnit tests for SettingsStore (JobHunt.Core/Settings/SettingsStore.cs) against TestDb: Load creates and persists a default AppSettings row when the table is empty and returns the lowest-Id row otherwise, Current caches (no second DB read), Save inserts when Id==0 and updates otherwise and refreshes the cache - the LLM provider, dry-run and daily apply cap all read through it | portfolio reset: stale: settings moved to MindAttic Vault (JH-T22) |
| JH-T7 | 2026-10-14 | chore | open | NUnit tests for JobPosting.AnnualizedSalary (JobHunt.Core/Jobs/JobPosting.cs): Hour x2080, Day x260, Week x52, Month x12, Year/unset x1, null Min/Max stay null, one-sided ranges - FitScorer's salary floor compares against this and only the hourly case is exercised indirectly | — |
| JH-T8 | 2026-10-14 | chore | wont-do | Move JobHunt.App's Serilog daily file (%LOCALAPPDATA%\MindAttic\JobHunt\Logs) to MindAttic.Log at the Vault path, carrying old logs forward | duplicate of JH-T33 |
| JH-T9 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| JH-T10 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 13 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| JH-T11 | 2026-10-16 | chore | closed | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | fb43a3a0dd6cf34c01823f99e7af490de3d1a823 |
| JH-T12 | 2026-10-16 | chore | wont-do | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | portfolio reset: generic test-run sweep |
| JH-T13 | 2026-10-16 | chore | wont-do | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | portfolio reset: generic cleanup sweep |
| JH-T14 | 2026-10-16 | docs | open | Bring README.md up to date with the code and the hunt-and-rank milestone (docs/RELEASE.md): what it is, setup, usage, current state, roadmap pointing at the next charter; follow codex-standard/README-STYLE.md | user request 2026-10-16: approved repo sweep |
| JH-T15 | 2026-10-16 | feature | closed | Log to MindAttic.Log (3.0.0): every app/CLI entry point writes to the standalone MindAttic.Log.db Vault assigns this app (VaultPaths.LoggingDbPath, MindAttic.Vault 6.0.0) through Serilog's WriteToMindAtticLog, tagged with its Component; log startup, user actions that change data, failures and unhandled exceptions (UI thread, AppDomain, unobserved tasks); tests point the log at a temp file. Pattern: MindAttic.Workflow WorkflowLog (M.FLOW-T92), MindAttic.Orchestrator OrchestratorLog | portfolio reset: container; children JH-T33..T35 re-parented under JH-T49 |
| JH-T16 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
| JH-T17 | 2026-10-19 | chore | wont-do | Convert tracked source files to LF line endings via .gitattributes, per HOUSE-LAW-13 (low priority) | — |
| JH-T18 | 2026-10-20 | docs | closed | Publish JobHunt docs to GitHub Pages via the shared Agency DocFX pipeline: run adopt-docs-site.ps1 (README.md as home page, docs/*.md as articles, API reference only where the code builds in CI), enable Pages with build_type=workflow, set the repo homepage URL, retire tools/build-readme.ps1 and README.htm, confirm the docs Actions run is green and https://mindattic.github.io/JobHunt/ returns 200. Depends on MAT-T8. | — |
| JH-T19 | 2026-10-20 | chore | closed | Move to MindAttic.Components.Wpf 19.0.0 (Mc->Ma rename): bump the PackageReference and rename every Mc-prefixed style/class reference to Ma in JobHunt.App/MainWindow.xaml and MainWindow.xaml.cs (~11 occurrences) | 11f5ce7 |
| JH-T20 | 2026-10-22 | docs | closed | Update USER_STORIES.md | — |
| JH-T21 | 2026-10-24 | docs | closed | Update USER_STORIES.md | — |
| JH-T22 | 2026-10-31 | feature | closed | LAMP: move JobHunt's app-level settings and keys to MindAttic.Vault.Desktop (manifest, LoadOrSeed, shared named keys, cog opens Vault) | 24f105e LampSettingsTests ByokKeysTests (146/146) |
| JH-T23 | 2026-10-31 | fix | closed | run.ps1 doesn't run the published exe with --write-settings-manifest (LAMP playbook step 7), so after a redeploy MindAttic Vault keeps showing the old settings manifest until the app next runs (found verifying M.VALT-T73) | b6ef574 LampSettingsTests.RunPs1_WritesTheSettingsManifestAfterPublishing |
| JH-T24 | 2026-10-31 | docs | closed | Update USER_STORIES.md | — |
| JH-T25 | 2026-11-02 | docs | closed | Adopt MindAttic.Agency docs-site pipeline, publish README to GitHub Pages (HOUSE-LAW-14) | — |
| JH-T26 | 2026-11-02 | fix | closed | Bughunt sweep, two passes: find and fix real bugs across tracked source (user request 2026-11-02) | — |
| JH-T27 | 2026-11-02 | fix | closed | Fix the open item the 2026-11-02 bughunt left for decision (Indeed page size from the live next-page link offset) | — |
| JH-T28 | 2026-11-03 | fix | closed | Check the Indeed board selectors (next-page link offset, pager end detection, from JH-T27) against a live Indeed search page and correct them | 1cf1504 (live check page 1 only; page 2 + last-page pager behind Cloudflare) |
| JH-T29 | 2026-11-03 | fix | closed | Indeed details.js challenge check reports a challenge on real job pages (same false positive JH-T28 fixed in results.js) and misses Cloudflare block pages | 9be873a |
| JH-T30 | 2026-11-10 | chore | closed | Crash recovery: commit the command-catalog sync (.agent/commands/*.md) and regenerated docs/USER_STORIES.md left uncommitted by the session crash | — |
| JH-T31 | 2026-11-10 | chore | closed | Adopt the shared docs-site pipeline: turn on GitHub Pages | — |
| JH-T32 | 2026-11-17 | docs | closed | Update USER_STORIES.md | — |
| JH-T33 | 2026-11-22 | feature | open | Add MindAttic.Log 3.0.0 and Serilog WriteToMindAtticLog configured from VaultPaths.LoggingDbPath with Component tag at every app/CLI entry point | — |
| JH-T34 | 2026-11-22 | feature | open | Log startup, data-changing user actions, failures, and unhandled exceptions (UI thread, AppDomain, unobserved tasks) | — |
| JH-T35 | 2026-11-22 | feature | open | Make tests point the log at a temp file and cover the logging setup | — |
| JH-T36 | 2026-11-22 | docs | closed | 2026-11-22 morning | — |
| JH-T37 | 2026-11-24 | fix | wont-do | Hunt the whole repo for real bugs with no time limit and fix every confirmed one | portfolio reset: generic bughunt sweep |
| JH-T38 | 2026-11-26 | fix | closed | BoardChallengeHeuristics treats any URL containing verify/blocked/challenge in its query or host as a CAPTCHA, so sign-in hands over to the user wrongly | 3f758ea |
| JH-T39 | 2026-11-26 | fix | closed | Indeed search URL sends jt=<first type> when several employment types are allowed, silently dropping the other types (e.g. contract roles) server-side | 0566331 |
| JH-T40 | 2026-11-26 | fix | closed | SettingsStore.Refresh runs on a Timer thread with no try/catch: a half-written Vault settings file throws and crashes the process | 16c19b9 |
| JH-T41 | 2026-11-26 | fix | closed | LinkedIn scripts match challenge/login words anywhere in the URL, so a job slug like /jobs/view/login-engineer-123 reads as signed out or a challenge | fb43a3a |
| JH-T42 | 2026-11-26 | feature | closed | JobHunt: adopt (or confirm) the shared run menu over deploy-lib.ps1, run Redeploy, and confirm its exe lands and starts at R:\MindAttic\Apps\JobHunt\Debug and \Release; repoint anything in the repo still naming an old deploy path. Outcome: both builds exist under R: and launch | already done: b96691c (MAT-T309) run menu; JobHunt.App.exe in R:\MindAttic\Apps\JobHunt\Debug and \Release |
| JH-T43 | 2026-11-26 | chore | open | Delete the old C:\Apps\JobHunt deploy now that R:\MindAttic\Apps\JobHunt runs from both Debug and Release. Outcome: the folder is gone | — |
| JH-T44 | 2026-11-27 | chore | open | Portfolio reset (MAT-T324): release charter (docs/RELEASE.md, one milestone), bible, and triage of every open ticket into epic>feature>task for: JobHunt, JoyceFit, ProcessLogger, Unstuck, RusNet | — |
| JH-T45 | 2026-11-27 | feature | open | The live LinkedIn scrape and the fit score's salary arithmetic are guarded by tests | — |
| JH-T46 | 2026-11-27 | feature | open | JobHunt logs to MindAttic.Log, deploys only from R:, and its README states the truth | — |
| JH-T47 | 2026-11-27 | feature | open | NUnit coverage of LinkedInSearcher results and listing flows with a fake browser surface | — |
| JH-T48 | 2026-11-27 | feature | open | NUnit coverage of JobPosting.AnnualizedSalary, which the fit score salary floor depends on | — |
| JH-T49 | 2026-11-27 | feature | open | Every JobHunt entry point logs to its MindAttic.Log database instead of the Serilog daily file | — |
| JH-T50 | 2026-11-27 | feature | open | README matches the code and the hunt-and-rank milestone | — |
| JH-T51 | 2026-11-27 | feature | open | JobHunt runs only from R:\MindAttic\Apps\JobHunt; the old C: deploy is removed | — |
