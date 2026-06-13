@echo off
REM publish-major.bat - publish a MAJOR update: X.0.0.0 bump (big releases,
REM e.g. 1.0.2.14 -> 2.0.0.0). Add the matching "## X.0.0.0" section to
REM changelog.txt first. Any extra arguments are forwarded to publish.ps1.

setlocal

set "SCRIPT_DIR=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish.ps1" -BumpPart Major %*
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
