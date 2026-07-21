# نظام الحضور والمرتبات — Hikvision DS-K1T342EX-E1

تطبيق ويب بـ **ASP.NET Core MVC (.NET 8)** لسحب بيانات الحضور من جهاز Hikvision عبر بروتوكول
**ISAPI** واستخدامها لإدارة الدوام وصرف المرتبات.

## الميزات
- إدارة **مجموعات التوظيف** (مثل: المدراء، الموظفون) مع خاصية التقيد بالوقت من عدمه.
- **مواعيد دوام** لكل مجموعة (حضور/انصراف، سماح تأخير، ساعات مطلوبة، أيام العمل).
  - المدراء: غير مقيّدين بوقت — بصمة دخول واحدة تكفي.
  - الموظفون: التقيد بحضور وانصراف. الدخول قبل الموعد مقبول، والخروج بعد الموعد مقبول.
- **إدخال سجلات حضور يدوية** موثّقة بنوعها وسببها: مهمة عمل / إجازة / خروج بإذن / إنجاز عمل،
  ومميَّزة بوضوح عن سجلات الجهاز.
- مفتاح **تبديل طريقة الاحتساب** لكل مجموعة: بأوقات الحضور أو بعدد ساعات العمل.
- **مزامنة يدوية** ("مزامنة الآن") تسحب البصمات من الجهاز وتمنع التكرار.
- **تقرير مرتبات** (حضور/غياب/تأخير/ساعات + تقدير الصرف) قابل للتصدير CSV/Excel.
- تسجيل دخول بمستخدم إداري واحد.

## المتطلبات
- .NET 8 SDK
- SQL Server (أو LocalDB للتطوير)
- أداة `dotnet-ef`: `dotnet tool install --global dotnet-ef`

## الإعداد والتشغيل
```bash
# 1) استعادة الحزم
dotnet restore

# 2) ضبط الاتصال وبيانات الجهاز في src/Hikvision.Web/appsettings.json
#    (ConnectionStrings:DefaultConnection و قسم Device)

# 3) إنشاء أول ترحيل ثم قاعدة البيانات
dotnet ef migrations add InitialCreate -p src/Hikvision.Web -s src/Hikvision.Web
dotnet ef database update -p src/Hikvision.Web -s src/Hikvision.Web

# 4) التشغيل
dotnet run --project src/Hikvision.Web
```

> ملاحظة: عند غياب أي ترحيلات، ينشئ التطبيق قاعدة البيانات تلقائيًا عبر `EnsureCreated`
> لتسهيل التجربة السريعة، لكن يُنصح باستخدام الترحيلات في الإنتاج.

عند أول تشغيل تُزرع تلقائيًا: مستخدم المدير (من قسم `Admin`)، مجموعتا "المدراء" و"الموظفون"
بوردياتهما الافتراضية، وصف إعدادات الجهاز.

بيانات الدخول الافتراضية: `admin` / كلمة المرور المضبوطة في `Admin:Password` (غيّرها فورًا).

## الاختبار دون جهاز
في وضع التطوير `Device:UseFakeClient = true` يُستخدم عميل وهمي يولّد بصمات تجريبية،
ما يتيح تجربة كامل المنظومة (المزامنة، الاحتساب، التقارير) دون جهاز فعلي. اضبطه على `false`
وأدخل بيانات جهاز حقيقي على الشبكة المحلية للاتصال الفعلي.

## بنية المشروع
```
src/Hikvision.Web/
  Models/            الكيانات والـ Enums
  Data/              AppDbContext و DbSeeder
  Services/          Hikvision (ISAPI) / Sync / Calculation / Reports / TimeZone
  Controllers/       Account, Home, Groups, Schedules, Employees,
                     Attendance, ManualAttendance, Sync, Reports, Settings
  Views/             واجهات Razor (RTL عربية)
```

## المزامنة الآلية الدورية
يضبط قسم `Sync` في `appsettings.json` المزامنة المجدولة:
```json
"Sync": { "Enabled": true, "IntervalMinutes": 15, "WindowStart": "08:00", "WindowEnd": "15:00" }
```
- تُنفَّذ المزامنة **كل 15 دقيقة** ضمن النافذة (8:00 حتى 15:00) منسّقة على فواصل الساعة
  (8:00، 8:15، 8:30...)، وكل مرة تسحب الجديد فقط منذ آخر مزامنة.
- بعد نهاية النافذة تُنفَّذ **مزامنة إغلاق** واحدة تلتقط بصمات الخروج المتأخرة.

## التشغيل التلقائي مع ويندوز (كخدمة Windows)
التطبيق يدعم العمل كخدمة Windows تبدأ مع إقلاع النظام. الخطوات (PowerShell كمسؤول):
```powershell
# 1) نشر التطبيق
dotnet publish src/Hikvision.Web -c Release -o C:\Hikvision\app

# 2) إنشاء الخدمة (تبدأ تلقائيًا مع ويندوز)
sc.exe create HikvisionAttendance binPath= "C:\Hikvision\app\Hikvision.Web.exe" start= auto
sc.exe description HikvisionAttendance "نظام الحضور والمرتبات"
sc.exe start HikvisionAttendance
```
لإيقاف/حذف الخدمة:
```powershell
sc.exe stop HikvisionAttendance
sc.exe delete HikvisionAttendance
```
> بديل أبسط (بدون خدمة): أنشئ مهمة في "جدولة المهام" (Task Scheduler) بمشغّل "عند بدء تشغيل الكمبيوتر" تشغّل `Hikvision.Web.exe`، أو ضع اختصارًا للملف في مجلد بدء التشغيل (Startup). تشغيله كخدمة هو الأنسب للإنتاج.

## الوصول للـ API من أجهزة أخرى على الشبكة المحلية
التطبيق يستمع على كل واجهات الشبكة عبر الإعداد:
```json
"Urls": "http://0.0.0.0:5005"
```
خطوات التفعيل على جهاز الخادم (ويندوز، PowerShell كمسؤول):
```powershell
# فتح المنفذ 5005 في جدار الحماية للوارد
netsh advfirewall firewall add rule name="Hikvision API" dir=in action=allow protocol=TCP localport=5005
```
ثم من الجهاز الآخر على نفس الشبكة، استبدل localhost بعنوان IP الخاص بجهاز الخادم:
```
http://192.168.95.50:5005/api/payroll/financial?month=3
```
(اعرف IP الخادم بأمر `ipconfig`.)

> 📑 **توثيق الـ API الكامل** (كل النقاط والمعاملات والأمثلة وصيغ الاستجابة) في ملف
> [`docs/API.md`](docs/API.md) — يشمل طلب التقرير بالشهر والسنة والقروب.

> 🔒 **مهم للأمان**: بما أن الـ API أصبح متاحًا على الشبكة، فعّل مفتاح الحماية في `appsettings.json`:
> ```json
> "Api": { "Key": "ضع-مفتاحًا-سريًا-قويًا" }
> ```
> عندها يجب أن ترسل المنظومة الخارجية الترويسة `X-Api-Key: ضع-مفتاحًا-سريًا-قويًا` مع كل طلب، وإلا تُرفض (401).

## ملاحظات
- أوقات الأحداث تُخزَّن بالمنطقة الزمنية المُعدّة في `Localization:TimeZone`.
- معادلة تقدير الصرف مبدئية (خصم عن أيام الغياب غير المبرّر) — راجِعها وفق سياسة منشأتك.
- كلمة مرور الجهاز تُخزَّن في قاعدة البيانات؛ للبيئات الحساسة فكّر في حمايتها عبر Data Protection.
