# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's own
documentation (README.md, docs/PLAN.md). Common prompt commands are implemented by the shared
runner; do not add a second copy under a provider-specific command folder.

Project context: JobHunt is standalone. It ports Automata's browser-automation techniques into
its own projects and must never take a project reference on Automata.

Watching the app while the person talks to you about it: JobHunt registers with AutoWebNav's Live
Observation (panel and board panes; preferred CDP ports 9366/9367, which tools/inspect-board.mjs
still uses). From the AutoWebNav repo, `node tools/awn-observe.mjs status jobhunt` (hunt state,
toast/hunt log, what the board page shows) and `watch jobhunt` (live event stream). Every
session's board actions are in Downloads as `JobHunt-session-*.autowebnav-recording.json` — this
replaced the per-board Spectator Mode button. Read, don't drive: the board is the applicant's
real account.
