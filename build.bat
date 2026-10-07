@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo       DTA PLAY TOGETHER - BUILD SYSTEM (C# / UNITY)
echo ========================================================

set ROOT=%~dp0
set DIST=%ROOT%dist

echo [1/4] Dang don dep thu muc dist cu...
if exist "%DIST%" (
    rd /s /q "%DIST%"
)
mkdir "%DIST%"

echo [2/4] Dang build va dong goi DTA_Client.exe (GUI)...
dotnet publish "%ROOT%src\Client.Gui\DTA_Client.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "%DIST%"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build DTA_Client that bai!
    exit /b %ERRORLEVEL%
)

echo [3/4] Dang build va dong goi DTA_Server.exe (GUI Host)...
dotnet publish "%ROOT%src\Server.Gui\DTA_Server.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "%DIST%"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build DTA_Server that bai!
    exit /b %ERRORLEVEL%
)

echo [4/4] Don dep file tam va hoan tat...
if exist "%DIST%\Client" rd /s /q "%DIST%\Client"
if exist "%DIST%\Server" rd /s /q "%DIST%\Server"

echo ========================================================
echo   BUILD THANH CONG! Ket qua tai: %DIST%
echo   - DTA_Client.exe (Giao dien dieu khien, WinExe khong co CMD)
echo   - DTA_Server.exe (Giao dien may chu, WinExe khong co CMD)
echo ========================================================
endlocal
