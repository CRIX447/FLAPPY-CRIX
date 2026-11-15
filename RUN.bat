@echo off
title Flappy Crix - Test run
cd /d "%~dp0"
call npx --yes @tauri-apps/cli@2.12.4 dev
