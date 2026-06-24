@echo off
setlocal EnableExtensions

echo ============================================================
echo   Publish Hikvision Attendance as a single Windows .exe
echo ============================================================
echo.

set "ROOT=%~dp0.."
set "OUT=%~dp0publish"

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK ^(dotnet^) was not found on this machine.
    echo         Install .NET 8 SDK first: https://dotnet.microsoft.com/download
    goto :end
)

echo Publishing ^(win-x64, self-contained, single file^)...
echo.
dotnet publish "%ROOT%\src\Hikvision.Web\Hikvision.Web.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%"
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Publish failed. See the messages above.
    goto :end
)

echo.
echo Done. Executable: %OUT%\Hikvision.Web.exe
echo Copy the whole "publish" folder to the Windows machine ^(e.g. C:\HikvisionApp^).

:end
echo.
echo Press any key to close this window...
pause >nul
endlocal
