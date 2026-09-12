@echo off
title Flappy Crix - Test
cd /d "%~dp0"

where node >nul 2>&1
if errorlevel 1 (
    echo  Node.js is not installed. Get it from nodejs.org
    pause
    exit /b
)

if not exist "node_modules" (
    echo  Installing dependencies, one moment...
    call npm install
)

echo  Starting Flappy Crix...
call npm start
