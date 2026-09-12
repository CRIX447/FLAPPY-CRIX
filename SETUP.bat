@echo off
title Flappy Crix - Setup
color 0B
cd /d "%~dp0"

echo.
echo  ================================================
echo    FLAPPY CRIX - ONE TIME SETUP
echo    CRIX STUDIOS
echo  ================================================
echo.
echo  This installs everything needed to build the game.
echo  You only run it once.
echo.
pause
cls

REM ============ NODE.JS ============
echo.
echo  [1/3] Checking Node.js...
echo.

where node >nul 2>&1
if not errorlevel 1 (
    for /f "delims=" %%v in ('node -v') do echo    Already installed: %%v
    goto :nodedone
)

echo    Not found. Installing it now.
echo.

REM winget ships with Windows 10 21H2 and later
where winget >nul 2>&1
if not errorlevel 1 (
    echo    Installing through winget...
    echo    Accept any prompt that appears.
    echo.
    winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
    goto :nodecheck
)

REM No winget - download the installer directly
echo    winget is unavailable, downloading the installer instead...
echo.
powershell -NoProfile -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$u='https://nodejs.org/dist/v20.18.0/node-v20.18.0-x64.msi';" ^
  "Write-Host '   Downloading Node.js (about 30MB)...';" ^
  "Invoke-WebRequest -Uri $u -OutFile \"$env:TEMP\node-setup.msi\";" ^
  "Write-Host '   Running the installer - accept the prompts...';" ^
  "Start-Process msiexec.exe -ArgumentList '/i',\"$env:TEMP\node-setup.msi\",'/qb' -Wait;" ^
  "Remove-Item \"$env:TEMP\node-setup.msi\" -ErrorAction SilentlyContinue"

:nodecheck
REM A fresh install is not on this window's PATH yet, so look for it directly
set "NODEPATH=%ProgramFiles%\nodejs"
if exist "%NODEPATH%\node.exe" set "PATH=%NODEPATH%;%PATH%"

where node >nul 2>&1
if errorlevel 1 (
    color 0E
    echo.
    echo  ================================================
    echo    ALMOST THERE
    echo  ================================================
    echo.
    echo  Node.js was installed, but this window cannot see
    echo  it yet - Windows only picks up new programs in a
    echo  fresh window.
    echo.
    echo  Close this window and run SETUP.bat again.
    echo.
    pause
    exit /b
)
for /f "delims=" %%v in ('node -v') do echo    Installed: %%v

:nodedone

REM ============ DEPENDENCIES ============
echo.
echo  [2/3] Installing what the build needs...
echo.

call npm install
if errorlevel 1 (
    color 0C
    echo.
    echo  [X] That failed. Usually a connection problem.
    echo      Check your internet and run this again.
    echo.
    pause
    exit /b
)
echo    Done.

REM ============ ICON ============
echo.
echo  [3/3] Checking for an icon...
echo.

if exist "icon.ico" (
    echo    icon.ico found.
) else (
    echo    No icon.ico in this folder.
    echo.
    echo    The build still works, it just uses the default
    echo    Electron icon. To use your own, put a real .ico
    echo    here - a renamed PNG will NOT work and makes the
    echo    build fail. Convert one at convertico.com
)

color 0A
echo.
echo  ================================================
echo    SETUP COMPLETE
echo  ================================================
echo.
echo  Next: run BUILD.bat to make the exe.
echo.
choice /c YN /m "  Run BUILD.bat now"
if not errorlevel 2 (
    start "" "%~dp0BUILD.bat"
)
