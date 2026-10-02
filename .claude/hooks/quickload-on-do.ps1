<#
  UserPromptSubmit hook: a bare "do" restores the last /quicksave handoff.

  Only the Claude Code glue lives here. Loading and archiving are the shared runner's `do`
  (..\mindattic-agent-standard\prose-agent.ps1), so this restores the same portable handoff every
  client uses. Any other prompt passes through untouched.

  Emits Claude Code hook JSON on stdout, escaped to pure ASCII (PowerShell 5.1 / Win-1252 safe).
  Emits {} whenever there is nothing to do.
#>
$ErrorActionPreference = 'Stop'

$raw = [Console]::In.ReadToEnd()
try { $j = $raw | ConvertFrom-Json } catch { $j = $null }
$prompt = if ($j -and $j.prompt) { [string]$j.prompt } else { '' }
if ($prompt -notmatch '^\s*(do|do it)\s*[.!]*\s*$') { Write-Output '{}'; exit 0 }

# <repo>\.claude\hooks\quickload-on-do.ps1 -> <repo>
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runner   = Join-Path (Split-Path -Parent $repoRoot) 'mindattic-agent-standard\prose-agent.ps1'
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.prose\agent-session.json')) -or -not (Test-Path -LiteralPath $runner)) {
  Write-Output '{}'; exit 0
}

# The runner prints the handoff and archives it (to .001, never deleted) in one step.
$body = (& powershell -NoProfile -ExecutionPolicy Bypass -File $runner do -RepoRoot $repoRoot | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($body)) { Write-Output '{}'; exit 0 }

$text = @'
RESUME CONTEXT (quicksave handoff, restored because the user typed "do"). The context window was
wiped since this was saved. Treat it as your working memory: pick up the Task, honor the
Decisions, and continue from Next without re-asking what was settled. Open by confirming in one
line what you're resuming. The handoff is archived (.prose\agent-session.json.001) and won't refill.

'@ + $body

$sb = New-Object System.Text.StringBuilder
foreach ($ch in $text.ToCharArray()) {
  $code = [int][char]$ch
  switch ($ch) {
    '"'  { [void]$sb.Append('\"') }
    '\'  { [void]$sb.Append('\\') }
    "`b" { [void]$sb.Append('\b') }
    "`f" { [void]$sb.Append('\f') }
    "`n" { [void]$sb.Append('\n') }
    "`r" { [void]$sb.Append('\r') }
    "`t" { [void]$sb.Append('\t') }
    default { if ($code -lt 32 -or $code -gt 126) { [void]$sb.Append('\u' + $code.ToString('x4')) } else { [void]$sb.Append($ch) } }
  }
}
Write-Output ('{"hookSpecificOutput":{"hookEventName":"UserPromptSubmit","additionalContext":"' + $sb.ToString() + '"}}')
exit 0
