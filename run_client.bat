@echo off
title DTA Client Hub - Play Together
echo ========================================================
echo  KHOI DONG DTA CLIENT HUB (C# Automation Engine)
echo ========================================================
dotnet run --project "%~dp0src\Client.Launcher\DTA.Launcher.csproj"
pause
