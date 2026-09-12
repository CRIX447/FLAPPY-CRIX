@echo off
title Flappy Crix - Build
color 0C
cd /d "%~dp0"

echo.
echo  ================================================
echo    FLAPPY CRIX - BUILD
echo    CRIX STUDIOS
echo  ================================================
echo.

REM ---- Node check ----
REM Pick up Node even if this window predates the install
if exist "%ProgramFiles%\nodejs\node.exe" set "PATH=%ProgramFiles%\nodejs;%PATH%"

where node >nul 2>&1
if errorlevel 1 (
    echo  [X] Node.js is not installed.
    echo.
    choice /c YN /m "  Install it automatically now"
    if errorlevel 2 (
        echo.
        echo  Get it manually from nodejs.org, take the LTS version.
        pause
        exit /b
    )
    echo.
    start "" /wait "%~dp0SETUP.bat"
    exit /b
)
for /f "delims=" %%v in ('node -v') do echo  Node %%v found

REM ---- icon check ----
if not exist "icon.ico" (
    echo.
    echo  [!] No icon.ico in this folder.
    echo      The build will use the default Electron icon.
    echo      Put a real .ico here to use your own - a renamed
    echo      PNG will NOT work and makes the build fail.
    echo.
    choice /c YN /m "  Carry on without a custom icon"
    if errorlevel 2 exit /b
    echo.
)

REM ---- dependencies ----
if not exist "node_modules" (
    echo.
    echo  Installing dependencies - a few minutes the first time...
    echo.
    call npm install
    if errorlevel 1 (
        echo.
        echo  [X] npm install failed. Check your internet connection.
        pause
        exit /b
    )
)

REM ---- clear a broken signing cache from an earlier attempt ----
if exist "%LOCALAPPDATA%\electron-builder\Cache\winCodeSign" (
    echo.
    echo  Clearing a partly extracted signing cache...
    rmdir /s /q "%LOCALAPPDATA%\electron-builder\Cache\winCodeSign" 2>nul
)

REM ---- clean old output ----
if exist "dist" (
    echo.
    echo  Clearing the last build...
    rmdir /s /q dist 2>nul
)

REM Stops electron-builder fetching the code-signing toolkit, which
REM contains macOS symlinks that Windows cannot extract without admin.
REM We do not sign anything, so it is not needed.
set CSC_IDENTITY_AUTO_DISCOVERY=false
set WIN_CSC_LINK=
set CSC_LINK=

echo.
echo  ================================================
echo    Building - this takes a couple of minutes
echo  ================================================
echo.

call npm run build
set BUILD_RESULT=%errorlevel%

REM ---- if the installer failed, try portable ----
if not exist "dist\*.exe" (
    echo.
    echo  [!] No exe produced. Trying the portable build instead,
    echo      which skips NSIS and usually works.
    echo.
    call npm run build:portable
)

echo.
echo  ================================================

if exist "dist\*.exe" (
    color 0A
    echo    BUILD FINISHED
    echo  ================================================
    echo.
    echo  Files ready to upload:
    echo.
    for %%f in (dist\*.exe) do echo    %%~nxf   %%~zf bytes
    if exist "dist\latest.yml" echo    latest.yml   ^(needed for auto-update^)
    echo.
    echo  ------------------------------------------------
    echo   Upload the files above to your GitHub release.
    echo.
    echo   Do NOT upload the exe inside win-unpacked. That
    echo   one needs every file around it and will fail with
    echo   a missing ffmpeg.dll error.
    echo  ------------------------------------------------
    echo.
    choice /c YN /m "  Open the dist folder now"
    if not errorlevel 2 explorer "%cd%\dist"
) else (
    color 0C
    echo    BUILD FAILED
    echo  ================================================
    echo.
    echo  No exe was produced. Look at the red text above:
    echo.
    echo   "Cannot create symbolic link"
    echo      Windows blocking the code signing toolkit.
    echo      Turn on Developer Mode:
    echo        Settings ^> System ^> For developers ^> Developer Mode
    echo      Or right click this file and Run as administrator.
    echo.
    echo   "icon.ico" or "reserved header"
    echo      Not a real .ico file. Convert properly at
    echo      convertico.com, or delete it and run again.
    echo.
    echo   "ENOTFOUND" or "ETIMEDOUT"
    echo      No internet, or a firewall blocking the download.
    echo.
    echo   Anything else
    echo      Try PORTABLE.bat - it skips the packaging step
    echo      that usually causes this.
    echo.
    echo  Scroll up and read the red error text - it says
    echo  exactly what stopped it.
    echo.
)

pause
