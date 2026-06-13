@echo off
REM publish-minor.bat - publish a MINOR update: 1.X.0.0 bump (new features,
REM e.g. 1.0.2.14 -> 1.1.0.0). Add the matching "## X.Y.0.0" section to
REM changelog.txt first. Any extra arguments are forwarded to publish.ps1.

setlocal

set "SCRIPT_DIR=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish.ps1" -BumpPart Minor %*
set "EXITCODE=%ERRORLEVEL%"

echo.
if %EXITCODE% NEQ 0 (
    echo Publish FAILED with exit code %EXITCODE%.
) else (
    echo Publish completed.
)
echo.
pause
exit /b %EXITCODE%
