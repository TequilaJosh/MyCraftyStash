@echo off
REM publish-republish.bat - re-publish the CURRENT version, no version bump.
REM Use after a failed/interrupted run, or to rebuild the same version. If the
REM GitHub release for this version already exists the release step will fail
REM on purpose (delete it first with "gh release delete vX.Y.Z.W" to replace).
REM Any extra arguments are forwarded to publish.ps1.

setlocal

set "SCRIPT_DIR=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish.ps1" -SkipBump %*
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
