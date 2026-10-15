---
codex: 1
project: JobHunt
code: JH
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

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
| JH-T3 | 2026-10-13 | feature | open | Move to MindAttic.Components.Wpf 5.0.0 and turn on McWindow.DarkTitleBar for its windows | MindAttic.Components.Wpf 5.0.0 |
| JH-T4 | 2026-10-14 | chore | open | NUnit tests for LinkedInSearcher browser flow (JobHunt.Core/Boards/LinkedIn/LinkedInSearcher.cs) with a fake IBrowserSurface + NoPacer: OpenResultsAsync maps challenge/signedOut/noResults states, returns Unrecognized after ResultsTimeout, scrolls until AtBottom or MaxScrollSteps, drops blank-id cards and undoubles text; OpenListingAsync clicks the card target else navigates to ViewUrl, ignores a pane showing a different job id, returns null on DetailsTimeout, throws BoardChallengeException/BoardSignedOutException; embedded scripts load - these guard the live LinkedIn scrape | — |
| JH-T5 | 2026-10-14 | chore | open | NUnit tests for IndeedSearcher browser flow (JobHunt.Core/Boards/Indeed/IndeedSearcher.cs) with a fake IBrowserSurface + NoPacer: OpenResultsAsync state mapping (challenge/signedOut/noResults/Unrecognized on timeout), scroll loop bounds, card filtering; OpenListingAsync card-click vs /viewjob?jk= fallback, stale-pane id mismatch, null on DetailsTimeout, BoardChallengeException/BoardSignedOutException; Indeed embedded scripts load - only Map/ParseWorkplace are covered today | — |
| JH-T6 | 2026-10-14 | chore | open | NUnit tests for SettingsStore (JobHunt.Core/Settings/SettingsStore.cs) against TestDb: Load creates and persists a default AppSettings row when the table is empty and returns the lowest-Id row otherwise, Current caches (no second DB read), Save inserts when Id==0 and updates otherwise and refreshes the cache - the LLM provider, dry-run and daily apply cap all read through it | — |
| JH-T7 | 2026-10-14 | chore | open | NUnit tests for JobPosting.AnnualizedSalary (JobHunt.Core/Jobs/JobPosting.cs): Hour x2080, Day x260, Week x52, Month x12, Year/unset x1, null Min/Max stay null, one-sided ranges - FitScorer's salary floor compares against this and only the hourly case is exercised indirectly | — |
| JH-T8 | 2026-10-14 | chore | open | Move JobHunt.App's Serilog daily file (%LOCALAPPDATA%\MindAttic\JobHunt\Logs) to MindAttic.Log at the Vault path, carrying old logs forward | — |
| JH-T9 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | WF-T14 |
