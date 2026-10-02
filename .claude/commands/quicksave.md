---
description: Save the current discussion as a handoff so it survives /clear; restore with a bare 'do' or /quickload.
argument-hint: "[optional note to emphasize what matters most]"
allowed-tools: PowerShell
---

# Quicksave

An alias for the shared MindAttic runner's `save` (`..\mindattic-agent-standard\prose-agent.ps1`).
The runner owns the file and its archiving; this alias only fills in what to save.

Write four fields about the *current* discussion — what a fresh session needs to resume without
re-asking anything. Be concrete: file paths, names, ids, exact commands. If `$ARGUMENTS` is
non-empty, lead **Task** with it.

- **Task** — the one thing we are mid-work on, as a resumable instruction.
- **Decisions** — what is settled and must not be re-litigated.
- **State** — what's done, what's in flight, the last action and its result.
- **Next** — the next concrete steps, numbered, the very next action first.

Run it from the JobHunt root with the PowerShell tool. Use single-quoted strings, doubling any `'`
inside them:

```powershell
& ..\mindattic-agent-standard\prose-agent.ps1 save -RepoRoot (Get-Location) `
  -Task '<task>' -Decisions '<decisions>' -State '<state>' -Next '<next>'
```

The runner never overwrites: an unconsumed earlier save is archived to
`.prose\agent-session.json.001` (older ones shift up) before the new one is written.

Then tell the user in one line that it's saved: run `/clear`, then type `do` (or `/quickload`).
Do nothing else.
