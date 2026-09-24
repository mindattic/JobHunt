@echo off
rem JobHunt redeploy convenience launcher.
rem Shuts down any running instance, clears build cache, rebuilds, publishes Release to
rem C:\Apps\JobHunt\, and launches that deployed copy. Same process Automata and KdpPublish use
rem to always run a fresh, independent deployed copy instead of a possibly-stale one.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" -Launch %*
