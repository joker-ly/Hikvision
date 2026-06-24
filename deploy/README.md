# تشغيل البرنامج على ويندوز كملف تنفيذي + خدمة تبدأ مع النظام

هذا الدليل يحوّل المشروع إلى ملف تنفيذي واحد (`.exe`) يعمل على ويندوز، يُفتح للشبكة
المحلية، ويبدأ تلقائيًا عند إقلاع النظام.

## المتطلب الوحيد على جهاز التشغيل: قاعدة بيانات SQL Server
البرنامج يستخدم **SQL Server** (سلسلة الاتصال في `appsettings.json`:
`Server=localhost;Database=HikvisionAttendance;Trusted_Connection=True`).

- ثبّت **SQL Server Express** (مجاني) على جهاز ويندوز إن لم يكن موجودًا.
- **الجداول تُنشأ تلقائيًا** عند أول تشغيل (ترحيلات EF Core) — لا حاجة لإنشاء يدوي.
- إن كانت النسخة باسم `SQLEXPRESS`، عدّل سلسلة الاتصال إلى:
  `Server=localhost\SQLEXPRESS;Database=HikvisionAttendance;Trusted_Connection=True;TrustServerCertificate=True`
- بما أن الخدمة تعمل افتراضيًا بحساب `NT AUTHORITY\SYSTEM`، تأكد أن لهذا الحساب
  صلاحية دخول على SQL Server (أو استخدم مصادقة SQL في سلسلة الاتصال:
  `Server=localhost;Database=HikvisionAttendance;User Id=sa;Password=كلمتك;TrustServerCertificate=True`).

> ملاحظة: لا حاجة لتثبيت .NET على جهاز التشغيل — النشر مضمّن (self-contained).

---

## الخطوات

### 1) إنشاء الملف التنفيذي (على جهاز فيه .NET 8 SDK)
شغّل:
```
deploy\publish.bat
```
يُنتج المجلد `deploy\publish` وبداخله `Hikvision.Web.exe` وملف `appsettings.json`.

### 2) النقل والإعداد على جهاز ويندوز
1. انسخ **كامل** محتويات `deploy\publish` إلى مجلد على الجهاز، مثلًا `C:\HikvisionApp`.
2. افتح `C:\HikvisionApp\appsettings.json` وعدّل:
   - `ConnectionStrings:DefaultConnection` بحسب SQL Server لديك.
   - `Device:Host/Username/Password` ببيانات جهاز البصمة.
   - `Localization:TimeZone` (مثلًا `Africa/Cairo`).
   - `Admin:Username/Password` لحساب المدير الأول.
   - `Urls` لتغيير المنفذ إن رغبت (الافتراضي `http://0.0.0.0:5005`؛
     يستمع على كل الشبكة تلقائيًا).

### 3) التثبيت كخدمة + فتح المنفذ (بنقرة واحدة)
انقر بزر الفأرة الأيمن على `install-service.bat` ثم **Run as administrator**.
يقوم تلقائيًا بـ:
- فتح المنفذ `5005` في جدار الحماية للوصول من أجهزة الشبكة.
- إنشاء خدمة `HikvisionAttendance` بنوع بدء **تلقائي** (تعمل مع إقلاع النظام).
- إعادة تشغيلها تلقائيًا عند أي تعطّل، وتشغيلها فورًا.

> إن غيّرت مجلد التثبيت أو المنفذ، عدّل المتغيرات `APPDIR` و`PORT` أعلى السكربت.

### 4) الوصول للبرنامج
- من نفس الجهاز: `http://localhost:5005`
- من أي جهاز على الشبكة: `http://[عنوان-IP-للجهاز]:5005`
  (اعرف العنوان بأمر `ipconfig`).

---

## أوامر مفيدة
| الغرض | الأمر |
|------|------|
| حالة الخدمة | `sc query HikvisionAttendance` |
| إيقاف/تشغيل | `sc stop HikvisionAttendance` / `sc start HikvisionAttendance` |
| إزالة كل شيء | تشغيل `uninstall-service.bat` كمسؤول |
| متابعة الأخطاء | عارض الأحداث Event Viewer ← Windows Logs ← Application |

## ملاحظات
- البرنامج يستمع عبر **HTTP** فقط على الشبكة المحلية؛ هذا مناسب لشبكة داخلية موثوقة.
- بعد أي تحديث للكود: أعد تنفيذ `publish.bat`، أوقف الخدمة، انسخ الملفات الجديدة،
  ثم شغّل الخدمة (`sc stop` ← نسخ ← `sc start`).
