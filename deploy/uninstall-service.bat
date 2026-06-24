@echo off
chcp 65001 >nul
REM ============================================================
REM  إزالة خدمة Windows وحذف قاعدة جدار الحماية.
REM  *** يجب تشغيله كمسؤول (Run as administrator) ***
REM ============================================================
setlocal
set SVCNAME=HikvisionAttendance
set PORT=5005

net session >nul 2>&1
if errorlevel 1 (
    echo شغّل هذا الملف كمسؤول: "Run as administrator".
    pause
    exit /b 1
)

echo === إيقاف وحذف الخدمة ===
sc stop %SVCNAME% >nul 2>&1
sc delete %SVCNAME%

echo === حذف قاعدة جدار الحماية ===
netsh advfirewall firewall delete rule name="Hikvision Attendance %PORT%" >nul 2>&1

echo.
echo تمت الإزالة.
pause
endlocal
