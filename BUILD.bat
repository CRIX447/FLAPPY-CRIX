@echo off
title Flappy Crix - Build
cd /d "%~dp0"
where cargo >nul 2>&1 || (echo  Rust is not installed. Get it from rustup.rs, then run this again. & pause & exit /b)
where npx >nul 2>&1 || (echo  Node.js is not installed. Get it from nodejs.org, then run this again. & pause & exit /b)
echo  Building Flappy Crix (the first build takes a few minutes)...
call npx --yes @tauri-apps/cli@2.12.4 build
echo.
echo  Installer: src-tauri\target\release\bundle\nsis\
echo  Portable:  src-tauri\target\release\flappy-crix.exe
pause
