@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

echo ====================================================================
echo   [DTA] PLAYTOGETHER NATIVE C++20 - AUTO BUILD AND PACKAGING DIST
echo   Mo Hinh: 100%% C++20 Native - Zero-Tap IL2CPP - Server-Driven UI
echo ====================================================================
echo.

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

:: 1. Tim duong dan CMake
set "CMAKE_EXE="
where cmake >nul 2>nul
if %ERRORLEVEL% equ 0 (
    set "CMAKE_EXE=cmake"
)

if "%CMAKE_EXE%"=="" (
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" (
        set "CMAKE_EXE=C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    )
)

if "%CMAKE_EXE%"=="" (
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" (
        set "CMAKE_EXE=C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    )
)

if "%CMAKE_EXE%"=="" (
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" (
        set "CMAKE_EXE=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    )
)

if "%CMAKE_EXE%"=="" (
    if exist "C:\Program Files\CMake\bin\cmake.exe" (
        set "CMAKE_EXE=C:\Program Files\CMake\bin\cmake.exe"
    )
)

if "%CMAKE_EXE%"=="" (
    echo [ERROR] Khong tim thay cmake.exe tren he thong!
    echo Vui long cai dat CMake hoac Visual Studio 2022 C++ Desktop Development.
    pause
    exit /b 1
)

echo [*] Su dung CMake tai: "%CMAKE_EXE%"
echo.

:: 2. Giai phong process cu neu dang chay
echo [*] Giai phong process cu neu dang chay...
taskkill /F /IM DTA_Playtogether.exe >nul 2>nul
taskkill /F /IM DTA_Server.exe >nul 2>nul
taskkill /F /IM dta_client.exe >nul 2>nul
taskkill /F /IM dta_server.exe >nul 2>nul

:: 3. Khoi tao va dong bo cau hinh CMake build
echo [*] Dong bo va tao cau hinh CMake build...
"%CMAKE_EXE%" -B build -S .
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Cau hinh CMake that bai!
    pause
    exit /b %ERRORLEVEL%
)

:: 4. Tien hanh Bien dich Release Targets
echo.
echo [*] Dang bien dich Release: DTA_Playtogether.exe va DTA_Server.exe...
"%CMAKE_EXE%" --build build --config Release
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Bien dich that bai! Vui long kiem tra loi code o tren.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [+] Bien dich Release hoan tat 100%%!
echo.

:: 5. Dong goi vao thu muc dist
echo [*] Dang dong goi sach se vao thu muc dist (KHONG FILE BAT, KHONG FILE LOG)...

set "DIST_CLIENT=%SCRIPT_DIR%dist\Client"
set "DIST_SERVER=%SCRIPT_DIR%dist\Server"

:: Lam sach thu muc dist cu hoan toan
if exist "%DIST_CLIENT%" (
    rmdir /s /q "%DIST_CLIENT%" >nul 2>nul
)
mkdir "%DIST_CLIENT%"

if exist "%DIST_SERVER%" (
    rmdir /s /q "%DIST_SERVER%" >nul 2>nul
)
mkdir "%DIST_SERVER%"

:: 5.1 Sao chep binary Client: DUY NHAT 1 FILE DTA_Playtogether.exe
if exist "%SCRIPT_DIR%bin\Client\Release\DTA_Playtogether.exe" (
    copy /Y "%SCRIPT_DIR%bin\Client\Release\DTA_Playtogether.exe" "%DIST_CLIENT%\DTA_Playtogether.exe" >nul
) else if exist "%SCRIPT_DIR%bin\Client\Release\dta_client.exe" (
    copy /Y "%SCRIPT_DIR%bin\Client\Release\dta_client.exe" "%DIST_CLIENT%\DTA_Playtogether.exe" >nul
)

:: Sao chep thu muc data cho Client
if exist "%SCRIPT_DIR%data" (
    xcopy /E /I /Y "%SCRIPT_DIR%data" "%DIST_CLIENT%\data" >nul
)

:: 5.2 Sao chep binary Server: DUY NHAT 1 FILE DTA_Server.exe
if exist "%SCRIPT_DIR%bin\Server\Release\DTA_Server.exe" (
    copy /Y "%SCRIPT_DIR%bin\Server\Release\DTA_Server.exe" "%DIST_SERVER%\DTA_Server.exe" >nul
) else if exist "%SCRIPT_DIR%bin\Server\Release\dta_server.exe" (
    copy /Y "%SCRIPT_DIR%bin\Server\Release\dta_server.exe" "%DIST_SERVER%\DTA_Server.exe" >nul
)

:: Sao chep thu muc data cho Server
if exist "%SCRIPT_DIR%data" (
    xcopy /E /I /Y "%SCRIPT_DIR%data" "%DIST_SERVER%\data" >nul
)

:: 5.3 Quet don triet de moi file .bat va file .log trong dist (KHONG DE LAI BAT KY FILE LOG HAY BAT NAO)
del /f /q /s "%DIST_CLIENT%\*.bat" >nul 2>nul
del /f /q /s "%DIST_CLIENT%\*.log" >nul 2>nul

del /f /q /s "%DIST_SERVER%\*.bat" >nul 2>nul
del /f /q /s "%DIST_SERVER%\*.log" >nul 2>nul

echo.
echo ====================================================================
echo   DONG GOI HOAN TAT VAO THU MUC DIST CHUAN XAC THEO YEU CAU:
echo ====================================================================
echo.
echo   [1] CLIENT (NGUOI DUNG):
echo       - Thu muc: %DIST_CLIENT%
echo       - Duy nhat 1 file exe: DTA_Playtogether.exe
echo       - Du lieu: thu muc data\
echo       - Khi mo len: Hien ngay Popup Modal de user tu nhap License Key va nut Xac Nhan,
echo         hoac chon option Dung Ban Mien Phi (Free Tier - Chi ho tro cau ca co ban,
echo         ban tat ca ca, khong loc, khong cau ca bong 6-7, khong can nhanh, co khoa cam).
echo       - Hoan toan KHONG co file .bat, KHONG co file .log, KHONG co man hinh console den.
echo.
echo   [2] SERVER (QUAN TRI VIEN / VPS):
echo       - Thu muc: %DIST_SERVER%
echo       - Duy nhat 1 file exe: DTA_Server.exe
echo       - Du lieu: thu muc data\
echo       - Khi mo len: Hien ngay Server Control Panel GUI (Tao key, Reset HWID,
echo         Quan ly Master Teleport Spots, Quan ly log).
echo       - Hoan toan KHONG co file .bat, KHONG co file .log, KHONG co man hinh console den.
echo.
echo ====================================================================
pause
