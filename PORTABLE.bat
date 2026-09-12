@echo off
title Flappy Crix - Portable Build
color 0A
cd /d "%~dp0"

echo.
echo  ================================================
echo    PORTABLE BUILD
echo  ================================================
echo.
echo  Makes a single self-contained exe. Skips the
echo  installer step, which is what usually fails on
echo  Windows because of the code signing toolkit.
echo.

if exist "%ProgramFiles%\nodejs\node.exe" set "PATH=%ProgramFiles%\nodejs;%PATH%"

where node >nul 2>&1
if errorlevel 1 (
    echo  [X] Node.js missing. Run SETUP.bat first.
    pause
    exit /b
)

REM No signing, so no toolkit download, so no symlink problem
set CSC_IDENTITY_AUTO_DISCOVERY=false

if not exist "node_modules" (
    echo  Installing dependencies...
    call npm install
)

echo.
echo  Building...
echo.
call npx electron-builder --win portable --publish never

echo.
if exist "dist\*.exe" (
    echo  ================================================
    echo    DONE
    echo  ================================================
    echo.
    for %%f in (dist\*.exe) do echo    %%~nxf   %%~zf bytes
    echo.
    echo  That file is self-contained. Upload it to your
    echo  GitHub release as FlappyCrix.exe
    echo.
    choice /c YN /m "  Open the folder"
    if not errorlevel 2 explorer "%cd%\dist"
) else (
    color 0C
    echo  Still failed. Paste the red text above and I can
    echo  tell you what it means.
    echo.
)
pause
