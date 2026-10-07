@echo off
title DTA Server - Play Together
echo ========================================================
echo  KHOI DONG DTA SERVER (ASP.NET Core C#)
echo  Endpoint: http://localhost:5000
echo  Swagger UI: http://localhost:5000/swagger
echo ========================================================
dotnet run --project "%~dp0src\Server\DTA.Server.csproj"
pause
