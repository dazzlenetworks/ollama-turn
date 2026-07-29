:: Ollama Turn CLI shim — forwards to ollama-turn.ps1

:: Created 7/28/2026


@echo off

setlocal


:: Wrapper: no chatter and no pause, so stdout stays clean for the caller.
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0ollama-turn.ps1" %*

exit /b %errorlevel%
