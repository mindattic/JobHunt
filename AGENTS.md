# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's own
documentation (README.md, docs/PLAN.md). Common prompt commands are implemented by the shared
runner; do not add a second copy under a provider-specific command folder.

Project context: JobHunt is standalone. It ports Automata's browser-automation techniques into
its own projects and must never take a project reference on Automata.
