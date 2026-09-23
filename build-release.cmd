@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%BUILD_EXIT_CODE%"=="0" (
  echo Build failed. Exit code: %BUILD_EXIT_CODE%
) else (
  echo Build completed successfully.
)
pause
exit /b %BUILD_EXIT_CODE%
