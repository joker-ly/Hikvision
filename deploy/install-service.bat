@echo off
chcp 65001 >nul
REM ============================================================
REM  تثبيت البرنامج كخدمة Windows تعمل تلقائيًا عند إقلاع النظام
REM  + فتح المنفذ في جدار الحماية للوصول من أجهزة الشبكة المحلية.
REM  *** يجب تشغيله كمسؤول (Run as administrator) ***
REM ============================================================
setlocal

REM --- عدّل هذه القيم إن لزم ---
set APPDIR=C:\HikvisionApp
set SVCNAME=HikvisionAttendance
set PORT=5005
REM ------------------------------

set EXE=%APPDIR%\Hikvision.Web.exe

net session >nul 2>&1
if errorlevel 1 (
    echo شغّل هذا الملف كمسؤول: انقر بزر الفأرة الأيمن ثم "Run as administrator".
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo لم يُعثر على الملف التنفيذي: %EXE%
    echo انسخ مخرجات النشر (مجلد publish) إلى %APPDIR% أولًا.
    pause
    exit /b 1
)

echo === فتح المنفذ %PORT% في جدار الحماية (للشبكة المحلية) ===
netsh advfirewall firewall delete rule name="Hikvision Attendance %PORT%" >nul 2>&1
netsh advfirewall firewall add rule name="Hikvision Attendance %PORT%" dir=in action=allow protocol=TCP localport=%PORT%

echo === إنشاء خدمة ويندوز تبدأ تلقائيًا مع النظام ===
sc stop %SVCNAME% >nul 2>&1
sc delete %SVCNAME% >nul 2>&1
sc create %SVCNAME% binPath= "\"%EXE%\"" start= auto DisplayName= "Hikvision Attendance"
sc description %SVCNAME% "نظام حضور ورواتب Hikvision"
REM إعادة تشغيل الخدمة تلقائيًا عند أي تعطّل
sc failure %SVCNAME% reset= 86400 actions= restart/5000/restart/5000/restart/5000
sc start %SVCNAME%

echo.
echo تم التثبيت بنجاح.
echo افتح من نفس الجهاز:   http://localhost:%PORT%
echo ومن أجهزة الشبكة:     http://[عنوان-IP-للجهاز]:%PORT%
echo (لمعرفة عنوان IP نفّذ الأمر:  ipconfig )
echo.
pause
endlocal
