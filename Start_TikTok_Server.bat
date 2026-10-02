@echo off
title TikTok Live Stream Bridge Server
cd /d "%~dp0tools\tiktok-server"
echo =======================================================
echo Dang khoi dong TikTok Live Connector Server...
echo =======================================================
bun run server.js
pause
