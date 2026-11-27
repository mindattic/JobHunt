---
layer: release
status: living
---
# JobHunt — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A Windows desktop app (WPF + WebView2) that holds an applicant's profile once, hunts LinkedIn at a human pace in
the applicant's own signed-in browser, and ranks every job with an inspectable 0–100 fit score.

## 2. The milestone: JobHunt 1.0, hunt and rank {#milestone}
- **Milestone:** MS-63, "Hunt and rank: a trustworthy LinkedIn hunt with fit-scored Best Fits"
- **Published means:** internal tool on `R:\MindAttic\Apps\JobHunt\{Debug,Release}` via `run.ps1`.
- **The natural stopping point:** PLAN.md milestones 0–3 (foundation, applicants and requirements, profile,
  search engine) are complete and proven against live LinkedIn, the scrape and the salary arithmetic are guarded
  by tests, and the app logs to MindAttic.Log like the rest of the portfolio. That is a whole, useful tool: it
  finds and ranks jobs. LLM reading, documents and applying are the next charter, not this one.

## 3. Done when (acceptance checks)
1. build — `JobHunt.slnx` builds clean on main.
2. exists — `R:\MindAttic\Apps\JobHunt\Release\JobHunt.App.exe`, and `C:\Apps\JobHunt` does not exist.
3. signoff — the owner runs one live, signed-in LinkedIn hunt and Best Fits fills with scored jobs.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | JH-T45 Hunt reliability | The live scrape and the score are guarded by tests | JH-T47 LinkedIn searcher tests (990), JH-T48 Salary arithmetic tests (980) |
| 800 | JH-T46 Operations | Logs in MindAttic.Log, one deploy on R:, README true | JH-T49 MindAttic.Log logging (890), JH-T51 R:-only deploy (880), JH-T50 README current (870) |

## 5. Out of scope (not before this milestone)
- PLAN.md milestones 4–7: LLM job understanding, résumé/cover-letter generation, Apply (N) / Easy Apply, status
  tracking and scheduled hunts — the next charter.
- Paste-a-résumé prefill (milestone 2 leftover) — needs the LLM work above.
- Indeed beyond its current pilot (JH-T5 tests) — LinkedIn is the primary board.
- Generic sweeps: full test runs, /simplify cleanups, exhaustive bughunts.

## 6. Current verified state
- Foundation, applicants/requirements UI and search engine proven by unit tests, page-script checks and an
  end-to-end hunt against a LinkedIn stand-in (docs/PLAN.md milestones 0–3).
- Shared run menu; Debug and Release exes on R: (b96691c, MAT-T309).
- Preferences and LLM keys live in MindAttic Vault (24f105e, JH-T22).
- Challenge/sign-in detection hardened (fb43a3a JH-T41, 3f758ea JH-T38).

## 7. Decision record
- 2026-11-27 — charter drafted from README/PLAN.md/code/tickets; Legion vote: Approve, unanimous among voters that answered (plurality quorum reached), no dissent (7 of 9 voters answered; deepseek had no key).
- Triage: 16 open (job ticket JH-T44 excluded) → done 1 (JH-T42), duplicate 1 (JH-T8), wont-do 6, container closed 1 (JH-T15), kept 7, new 0.
