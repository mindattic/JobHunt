---
layer: bible
status: living
---
# JobHunt — Project Bible

> Single source of truth for what JobHunt IS, is NOT, and the rules that keep it coherent.
> README says how to build and run it; this says how to think about it. The finish line lives in
> `docs/RELEASE.md`.

## 1. The one sentence {#JH-§1}
Fill in your job-application details once; JobHunt hunts LinkedIn in your own signed-in browser and ranks every
job with a fit score you can inspect.

## 2. The product promise {#JH-§2}
- Type your profile, preferences and screening answers once, not once per application.
- Walk job-board results at a human pace while you do something else.
- Show why a job ranks where it does: hard filters with stated reasons, then a 0–100 weighted score.
- Keep several applicants apart, each with their own board sign-in.
- Your data stays local: one SQLite file, JSON import/export, keys in MindAttic Vault.

## 3. What it is NOT {#JH-§3}
- Not a scraper farm: one applicant's own browser, human-paced, stopping at once on any challenge or sign-out.
- Not dependent on Automata: browser automation comes from the AutoWebNav packages only.
- Not an auto-submitter by default: dry run is on and stops before the final Submit (when applying exists).
- Not an inventor of facts: tailoring chooses which profile facts to show, never makes new ones (RESUME_GUIDE).

## 4. Architecture canon {#JH-§4}
```
 JobHunt.App (WPF shell)                       JobHunt.Core
 ┌──────────────┬──────────────────┐          ┌─────────────────────────────┐
 │ panel        │ board pane       │  ──────► │ Hunting/HuntRunner          │
 │ (WebView2,   │ (WebView2, the   │          │ Boards/LinkedIn, Indeed     │
 │  wwwroot)    │  real board)     │          │ Scoring/FitScorer           │
 ├──────────────┴──────────────────┤          │ Profile, Jobs, Applications │
 │ native bar: Apply (N), DRY RUN  │          │ Data (EF Core + SQLite)     │
 └─────────────────────────────────┘          │ Settings (Vault), Llm       │
                                              └─────────────────────────────┘
```
### 4.1 Projects
- `JobHunt.App` — WPF + WebView2 shell: panel UI (`wwwroot`), board pane, native Apply bar.
- `JobHunt.Core` — domain, boards, hunt runner, scoring, storage, settings, LLM routing.
- `JobHunt.Tests` — NUnit tests; `tools/` — Node page-script and end-to-end hunt checks.

### 4.2 Domain model
Applicant (profile), Search (terms + board-agnostic filters + weights), JobPosting keyed by
`(BoardId, ExternalId)`, JobApplication, AppSettings.

### 4.3 Key services
`HuntRunner` (runs every search page by page), `IJobBoard` per site (LinkedIn primary, Indeed pilot),
`FitScorer` (filters then weighted score), profile/job stores, Vault-backed settings and BYOK keys.

## 5. The Laws {#JH-§5}
Inherits `MindAttic.HouseRules.md` by reference.
1. Stop at once on any CAPTCHA, checkpoint or sign-out; never try to get past one. {#JH-LAW-1}
2. Never touch a job the applicant applied to or dismissed. {#JH-LAW-2}
3. The LLM supplies facts; code does the arithmetic. An unstated field never fails a filter (half credit). {#JH-LAW-3}
4. Each board is one `IJobBoard`; profile, scoring and documents are shared across boards. {#JH-LAW-4}
5. Keys and passwords live in MindAttic Vault, never in the database or an export. {#JH-LAW-5}
6. No project reference on Automata. {#JH-LAW-6}

## 6. Verified state {#JH-§6}
PLAN.md milestones 0–3 proven by unit tests, page-script checks and an end-to-end hunt against a LinkedIn
stand-in; a live signed-in LinkedIn hunt is the remaining proof. Deployed to `R:\MindAttic\Apps\JobHunt`
(b96691c).

## 7. Active frontier {#JH-§7}
`docs/RELEASE.md` — milestone MS-63 "Hunt and rank". The longer product plan is `docs/PLAN.md`.

## 8. Quality bar {#JH-§8}
`JobHunt.slnx` builds clean; new board logic has NUnit coverage with a fake browser surface; anything
touching the live board is checked read-only (AutoWebNav Live Observation), never by driving the real account.

## 9. Glossary {#JH-§9}
- **Hunt** — one run of every search term across a board.
- **Best Fits** — the ranked list of jobs that passed the filters.
- **Dry run** — fills everything, stops before the final Submit.
- **BYOK** — bring your own LLM key (Claude, OpenAI, Gemini, Kimi).
