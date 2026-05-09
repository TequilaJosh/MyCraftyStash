@echo off
REM publish.bat - double-click wrapper for publish.ps1.
REM Runs the publish script with ExecutionPolicy Bypass so it works regardless
REM of the machine's PowerShell policy, then pauses so the output stays
REM visible after the script finishes (or fails).

setlocal

REM Always run the .ps1 sitting next to this .bat, regardless of where the
REM user double-clicks from.
set "SCRIPT_DIR=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish.ps1" %*
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
