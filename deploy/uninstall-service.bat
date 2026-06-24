@echo off
setlocal EnableExtensions

set "SVCNAME=HikvisionAttendance"
set "PORT=5005"

echo ============================================================
echo   Hikvision Attendance - Uninstall Windows Service
echo ============================================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script must be run as Administrator.
    echo         Right-click the file and choose "Run as administrator".
    goto :end
)

echo Stopping and deleting service "%SVCNAME%"...
sc stop %SVCNAME% >nul 2>&1
sc delete %SVCNAME%
echo.

echo Removing firewall rule for port %PORT%...
netsh advfirewall firewall delete rule name="Hikvision Attendance %PORT%" >nul 2>&1
echo.

echo Done.

:end
echo.
echo Press any key to close this window...
pause >nul
endlocal
