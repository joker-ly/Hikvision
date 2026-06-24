@echo off
setlocal EnableExtensions

REM ------- Settings (edit if needed) -------
set "APPDIR=C:\HikvisionApp"
set "SVCNAME=HikvisionAttendance"
set "PORT=5005"
REM -----------------------------------------
set "EXE=%APPDIR%\Hikvision.Web.exe"

echo ============================================================
echo   Hikvision Attendance - Install as Windows Service
echo ============================================================
echo.

REM --- Must run as Administrator ---
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script must be run as Administrator.
    echo         Right-click the file and choose "Run as administrator".
    goto :end
)

REM --- Executable must exist ---
if not exist "%EXE%" (
    echo [ERROR] Executable not found:
    echo         %EXE%
    echo         Copy the published output ^(deploy\publish^) into %APPDIR% first.
    goto :end
)

echo [1/3] Opening firewall port %PORT% for the local network...
netsh advfirewall firewall delete rule name="Hikvision Attendance %PORT%" >nul 2>&1
netsh advfirewall firewall add rule name="Hikvision Attendance %PORT%" dir=in action=allow protocol=TCP localport=%PORT%
echo.

echo [2/3] Creating Windows service "%SVCNAME%" (automatic start)...
sc stop %SVCNAME% >nul 2>&1
sc delete %SVCNAME% >nul 2>&1
sc create %SVCNAME% binPath= "\"%EXE%\"" start= auto DisplayName= "Hikvision Attendance"
sc description %SVCNAME% "Hikvision Attendance and Payroll System"
sc failure %SVCNAME% reset= 86400 actions= restart/5000/restart/5000/restart/5000
echo.

echo [3/3] Starting the service...
sc start %SVCNAME%
echo.

echo ============================================================
echo   Done.
echo   Local:   http://localhost:%PORT%
echo   Network: http://[THIS-PC-IP]:%PORT%   (run ipconfig to find IP)
echo ============================================================

:end
echo.
echo Press any key to close this window...
pause >nul
endlocal
