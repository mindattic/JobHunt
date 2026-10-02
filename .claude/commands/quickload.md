---
description: Restore the last /quicksave handoff and resume exactly where it left off.
argument-hint: "[archive number, e.g. 1 — omit or pass 0 for the live save]"
allowed-tools: PowerShell, Read
---

# Quickload

An alias for the shared MindAttic runner's `load` (`..\mindattic-agent-standard\prose-agent.ps1`).

## No argument, or `0`

Run from the JobHunt root with the PowerShell tool:

```powershell
& ..\mindattic-agent-standard\prose-agent.ps1 load -RepoRoot (Get-Location)
```

- If it reports no handoff exists, tell the user there's nothing to restore, and stop.
- Otherwise treat its Task / Decisions / State / Next (and any Queued items) as your working
  memory. The runner has already archived the save to `.prose\agent-session.json.001` — consumed
  for resume, never deleted. Confirm in one line what you're resuming, then continue from Next
  without re-asking anything already settled.

## A positive number — `/quickload <N>`

A read-only recall of an archived save. Zero-pad `N` to three digits and read
`.prose\agent-session.json.NNN` with the Read tool. Change nothing on disk.

- If that archive doesn't exist, list the `.prose\agent-session.json.*` numbers that do, and stop.
- If it exists, resume from it as above. If the live save or a newer archive describes a different
  task, say so.
