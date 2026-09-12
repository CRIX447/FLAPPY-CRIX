@echo off
title Flappy Crix - Publish
color 0E
cd /d "%~dp0"

echo.
echo  ================================================
echo    PUBLISH TO GITHUB
echo  ================================================
echo.
echo  Builds the game and creates the GitHub release
echo  in one step.
echo.
echo  You need:
echo    - a PUBLIC repo called crix-desktop
echo    - a personal access token with "repo" scope
echo      ^(GitHub ^> Settings ^> Developer settings^)
echo.

if "%GITHUB_TOKEN%"=="" (
    echo  No token set for this session.
    echo.
    set /p GITHUB_TOKEN="  Paste your GitHub token: "
    echo.
)

if "%GITHUB_TOKEN%"=="" (
    echo  [X] No token entered. Stopping.
    pause
    exit /b
)

REM ---- show what version is about to go out ----
for /f "tokens=2 delims=:, " %%v in ('findstr /C:"\"version\"" package.json') do (
    set VER=%%~v
    goto :gotver
)
:gotver
echo  About to publish version %VER%
echo.
echo  If that is not right, edit "version" in package.json first.
echo  GitHub rejects a release that reuses an existing tag.
echo.
choice /c YN /m "  Continue"
if errorlevel 2 exit /b

if not exist "node_modules" (
    echo.
    echo  Installing dependencies...
    call npm install
)

echo.
echo  Building and publishing...
echo.
call npm run publish

echo.
if errorlevel 1 (
    color 0C
    echo  ================================================
    echo    PUBLISH FAILED
    echo  ================================================
    echo.
    echo  Common causes:
    echo    - The repo does not exist, or is private
    echo    - The token is wrong or lacks "repo" scope
    echo    - This version was already released
    echo      ^(bump "version" in package.json^)
    echo.
) else (
    color 0A
    echo  ================================================
    echo    PUBLISHED
    echo  ================================================
    echo.
    echo  Check it here:
    echo    https://github.com/CRIX447/crix-desktop/releases
    echo.
    echo  Make sure the release shows a .exe AND latest.yml
    echo  under Assets. Without latest.yml, auto-update
    echo  will not work.
    echo.
    echo  Then check your download page picks it up:
    echo    https://crixgamingvr.com/download
    echo.
)
pause
