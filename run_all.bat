@echo off
title DTA Play Together - Khoi dong he thong
echo Dang khoi dong Server...
start "DTA Server" cmd /c "%~dp0run_server.bat"
timeout /t 3 /nobreak >nul
echo Dang khoi dong Client Hub...
start "DTA Client Hub" cmd /c "%~dp0run_client.bat"
