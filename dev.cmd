@echo off
rem All-in-one dev launcher for Windows: runs tools/dev/dev.sh with Git Bash. See: dev.cmd --help
setlocal
set "GITBASH="
rem Git Bash lives next to git.exe (<Git>\cmd\git.exe -> <Git>\bin\bash.exe); avoid WSL's bash.exe.
for /f "delims=" %%G in ('where git 2^>nul') do if not defined GITBASH if exist "%%~dpG..\bin\bash.exe" set "GITBASH=%%~dpG..\bin\bash.exe"
if not defined GITBASH if exist "%ProgramFiles%\Git\bin\bash.exe" set "GITBASH=%ProgramFiles%\Git\bin\bash.exe"
if not defined GITBASH (
  echo Serve Git for Windows ^(Git Bash^): https://git-scm.com/download/win
  exit /b 1
)
"%GITBASH%" "%~dp0tools/dev/dev.sh" %*
exit /b %ERRORLEVEL%
