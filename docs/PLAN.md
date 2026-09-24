# JobHunt — Plan

**Fill in your information one last time.** JobHunt holds every field that job applications ask
for, in one place. It searches job boards (LinkedIn first) across all the searches you set up,
keeps only the jobs that pass your filters, and ranks them with a fit score you can inspect. Each
recommendation comes with a plain-English description plus a `First-Last-Résumé.docx` and
`First-Last-Cover-Letter.docx` written for that exact job. Check the jobs you want and click the
big blue **Apply (N)**. JobHunt then fills in each Easy Apply form: in **dry run** (the default) it
stops just before Submit, and once dry run is off it submits for real.

LinkedIn has no public API for applying, and neither does Amazon KDP. So JobHunt, like
KdpPublish, drives the site inside the user's own signed-in browser.

## Decisions

| Area | Decision |
|---|---|
| Browser automation | **AutoWebNav** (github.com/mindattic/AutoWebNav): the shared library, extracted from Automata, that Automata, KdpPublish and JobHunt all consume as NuGet packages. JobHunt has no dependency on Automata itself. |
| Storage | **SQLite + EF Core** in one file, `%LocalAppData%\MindAttic\JobHunt\jobhunt.db`. Migrations run at startup, so upgrades keep user data. Nothing to install, so it's easy to share. |
| Documents | `.docx` files on disk under `Documents\JobHunt\Applications\<Company - Title (board-id)>\`, where the user can open and edit them in Word. The database stores their paths and generated hashes; an edited file is never overwritten without asking. |
| LLM | **Bring your own key** for Claude, OpenAI, Gemini or Kimi. The chosen provider runs first and the others are fallbacks. Keys are kept in MindAttic.Vault (`jobhunt-<provider>`), never in the database or an export. Calls route through MindAttic.Legion. |
| Logging | Serilog behind `ILogger<T>` writes daily rolling files to `%LocalAppData%\MindAttic\JobHunt\Logs`. Each application attempt also keeps its own step log (`JobApplication.RunLog`). |
| Safety | **Dry run is on by default.** A dry run fills everything, uploads both files, answers every question, then stops before the final click, and is recorded as a dry-run application. Also: a daily cap on real submissions, human-paced delays, and an immediate stop on a CAPTCHA or checkpoint. |
| More boards later | Each site is an `IJobBoard`. A posting is keyed by `(BoardId, ExternalId)`, while the profile, scoring and documents are shared across boards. Adding a site means writing one new board class. |
| Résumé quality | The rules come from `docs/RESUME_GUIDE.md` (researched from 37 sources): single-column, ATS-safe .docx; action-verb bullets; metrics only from the profile; tailoring by choosing which facts to include, never by inventing them. |

## What's stored

- **Applicant:**
  - contact details and address
  - work authorization (sponsorship, clearance, visa, background/drug-test consent, non-compete)
  - compensation (current, desired, minimum, hourly)
  - work preferences (titles, remote/hybrid/on-site, relocation, travel, time zones, employment types, industries, company blocklist, notice period, shifts/on-call)
  - summary, links
  - work history, with bullets, technologies, supervisor and reason for leaving
  - education, and skills (hard/soft/tool/domain, with years, level and aliases)
  - certifications, languages, projects, accomplishments (awards, publications, patents, volunteer work, talks, military service), references
  - voluntary EEO disclosures, which default to "decline"
  - three open-ended parts: **keyword lists**, **text blocks** and **custom fields**
  - the screening-question answer bank

  Import and export use one `.jobhunt-profile.json` file.
- **Jobs:**
  - the board, its job ID, URL, title, company and location
  - posting date, applicant count, and whether Easy Apply is available
  - the raw description and the **plain-English description**
  - workplace (remote/hybrid/on-site)
  - **employment type**, **contract duration** and contract terms (W2/1099/C2C)
  - salary (min, max, pay period)
  - sponsorship, clearance and staffing-agency flags
  - tech stack, and requirements with the profile facts that match them
  - the evidence quote behind each extracted fact
  - the score and its per-criterion breakdown, and the filter reasons
  - the documents, status, selection, and which searches found the job
- **Applications:**
  - date, dry-run flag, and method
  - the exact files uploaded, with their hashes
  - every question asked and the answer given
  - the step log
  - the outcome (submitted → viewed → in review → interviewing → offer / accepted / rejected / withdrawn / no response / closed), with a dated event history showing the source of each change (manual, the board, or the automation)
- **Searches:** search terms, board-agnostic filters (LinkedIn applies what it can server-side and JobHunt enforces all of them), score weights, threshold, a cap on documents per hunt, and a history of runs.
- **Settings:** LLM provider, dry run, daily cap, pre-flight question scan, action delays, and theme.

## Fit score (0–100)

Stage 1 is hard filters, each pass/fail with a stated reason. Stage 2 is a weighted score with
these default weights (all adjustable):

| Criterion | Weight |
|---|---|
| Required skills | 35 |
| Nice-to-have skills | 10 |
| Title match | 15 |
| Salary | 15 |
| Workplace | 10 |
| Employment type | 10 |
| Freshness | 5 |

The LLM supplies facts and code does the arithmetic. A field the posting leaves unstated never
fails a filter; it earns half credit instead.

## Milestones

Status per HOUSE-LAW-8: ✅ proven by build/tests · 🟡 partial · ⬜ not started.

0. ✅ **AutoWebNav** — extracted from Automata. 10 library tests pass. Automata builds on it with 556/556 tests and 13/13 JS checks. KdpPublish builds on it (no automated tests of its own, and no live KDP run yet).
1. ✅ **Foundation** — SQLite/EF schema and migration; the profile, job, application, search and settings models; profile import/export; LinkedIn search-URL builder covering every filter; fit scorer; `Résumé` file naming; BYOK key store and Legion routing; Serilog; WPF shell with the panel, the LinkedIn pane and the native bar (**Apply (N)** and the DRY RUN badge). 33 tests pass, and the app launches and migrates a fresh database.
1b. ✅ **Applicants & requirements UI** — multiple applicants (user table) with an applicant dropdown and
   Add / Edit / Delete (delete hides; restore from Settings); per-applicant LinkedIn session and
   sign-in (email in the profile, password DPAPI-encrypted in Vault, auto sign-in that stops at any
   verification challenge); the Applicant tab covering every profile section; the Job Requirements
   tab with a live preview of the LinkedIn searches it runs; whole-database backup/restore.
   51 unit tests + an 11-step end-to-end drive of the real app pass.
2. 🟡 **Profile editor** — done as part of 1b except: paste an existing résumé to prefill (LLM parse + review).
3. 🟡 **Search engine** — built: `HuntRunner` runs every term page by page at a human pace, clicks
   each card (trusted input), reads the details pane, de-duplicates across terms, never touches jobs
   already applied to or dismissed, re-reads details only after 3 days, stops at once on any
   challenge / sign-out, gives each job a provisional listing-level score, and fills Best Fits.
   Proven: 86 unit tests, 27 page-script checks on modelled LinkedIn markup (`verify-linkedin.mjs`),
   and an 11-check end-to-end hunt through the real app against a local LinkedIn stand-in
   (`verify-hunt.mjs`). **Open:** a live, signed-in LinkedIn run — LinkedIn's real markup is the
   one thing none of these can prove.
4. ⬜ **Job understanding** — one LLM call per job returning the plain description, employment type, duration, terms, salary, and requirements matched to profile facts with evidence. Then filters and scoring.
5. ⬜ **Documents** — DOCX generation following RESUME_GUIDE, a validator that rejects invented facts, and detection of files the user has edited.
6. ⬜ **Apply (N)** — the pre-flight question scan, the Easy Apply state machine (dry run stops before Submit), the queue, the "N questions need you" panel, and the daily cap.
7. ⬜ **Tracking and scheduling** — read application status back from LinkedIn's Applied page, set outcomes by hand, schedule daily hunts, export to CSV.
