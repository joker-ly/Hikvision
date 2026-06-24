@echo off
REM ============================================================
REM  نشر البرنامج كملف تنفيذي واحد لويندوز (64-bit) بدون الحاجة
REM  لتثبيت .NET على الجهاز الهدف (self-contained single file).
REM  شغّل هذا الملف على جهاز فيه .NET 8 SDK.
REM ============================================================
setlocal
set ROOT=%~dp0..
set OUT=%~dp0publish

echo === نشر Hikvision Attendance (win-x64, self-contained) ===
dotnet publish "%ROOT%\src\Hikvision.Web\Hikvision.Web.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUT%"

if errorlevel 1 (
    echo.
    echo *** فشل النشر ***
    pause
    exit /b 1
)

echo.
echo تم النشر بنجاح.
echo الملف التنفيذي:  %OUT%\Hikvision.Web.exe
echo انسخ كامل محتويات المجلد "%OUT%" إلى جهاز ويندوز (مثلًا C:\HikvisionApp).
echo.
pause
endlocal
