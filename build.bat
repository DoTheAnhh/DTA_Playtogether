@echo off
chcp 65001 >nul
rem Build ban phat hanh vao dist\ (Client + Server). Tham so them (vd: -Server host:port, -Secrets thu_muc, -Linux) duoc chuyen thang cho build.ps1.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 (
    echo.
    echo BUILD THAT BAI - xem loi o tren.
    pause
    exit /b 1
)
echo.
echo BUILD XONG.
pause
