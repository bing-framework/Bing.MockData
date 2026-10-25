@echo off
pwsh -NoProfile -File "%~dp0pack.ps1" %*
exit /b %errorlevel%
