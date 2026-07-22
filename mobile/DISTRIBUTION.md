# الدليل الكامل خطوة بخطوة: من الكود إلى هواتف الموظفين
## أندرويد (APK موقَّع) + iOS (TestFlight بإيميلات الموظفين)

> **جهازك جاهز إن توفّر**: ماك + Xcode + Flutter + Android Studio.
> تحقّق قبل البدء:
> ```bash
> flutter doctor
> ```
> يجب أن ترى ✓ أمام: Flutter، Android toolchain، Xcode. إن ظهر ✗ أمام Android:
> ```bash
> flutter doctor --android-licenses   # اضغط y لكل الرخص
> ```

---

# المرحلة 0: تجهيز المشروع (مرة واحدة فقط)

### الخطوة 0.1 — جلب آخر نسخة من الكود
```bash
cd ~/PhpstormProjects/Hikvision-claude-hikvision-attendance-payroll-qqh2qz
git pull
cd mobile
```

### الخطوة 0.2 — توليد مجلدات المنصات (android و ios)
```bash
flutter create . --project-name attendance_portal --org sa.gov.ministry
flutter pub get
```
> نتيجة متوقعة: ظهور مجلدي `android/` و `ios/` داخل `mobile/`.
> معرّف الحزمة الناتج: `sa.gov.ministry.attendance_portal` — **لا تغيّره لاحقًا أبدًا**.

### الخطوة 0.3 — تعديل AndroidManifest.xml
افتح الملف:
```bash
open -a "Android Studio" android/app/src/main/AndroidManifest.xml
```
عدّل وسم `<application` ليصبح **بهذا الشكل بالضبط** (سطرا `label` و`usesCleartextTraffic`):
```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application
        android:label="حضوري"
        android:name="${applicationName}"
        android:icon="@mipmap/ic_launcher"
        android:usesCleartextTraffic="true">
        <activity
            android:name=".MainActivity"
            ...بقية الملف كما ولّده flutter دون تغيير...
        </activity>
        ...
    </application>
</manifest>
```
> ⚠️ لا تحذف أي شيء ولّده Flutter — فقط أضف/عدّل الخاصيتين:
> - `android:label="حضوري"` (اسم التطبيق تحت الأيقونة)
> - `android:usesCleartextTraffic="true"` (السماح بالاتصال بخادمك http الداخلي)

وأضف **قبل** وسم `<application` (لصلاحية الدخول بالبصمة):
```xml
    <uses-permission android:name="android.permission.USE_BIOMETRIC"/>
```

### الخطوة 0.3ب — تفعيل البصمة (MainActivity)
ميزة الدخول بالبصمة تتطلب تغيير الصنف الأساسي. افتح:
`android/app/src/main/kotlin/sa/gov/ministry/attendance_portal/MainActivity.kt`
وغيّر محتواه إلى:
```kotlin
package sa.gov.ministry.attendance_portal

import io.flutter.embedding.android.FlutterFragmentActivity

class MainActivity : FlutterFragmentActivity()
```
> التغيير الوحيد: `FlutterActivity` ← `FlutterFragmentActivity` (سطر الاستيراد والوراثة).
> بدون هذا التغيير سيتعطّل التطبيق عند طلب البصمة على أندرويد.

### الخطوة 0.3ج — توليد أيقونات التطبيق (مرة واحدة)
الأيقونة جاهزة في `assets/icon/` (بصمة على تدرّج أزرق). لتوليد كل المقاسات
لأندرويد وiOS تلقائيًا:
```bash
flutter pub get
dart run flutter_launcher_icons
```
نتيجة متوقعة: `✓ Successfully generated launcher icons`.
> أعد هذا الأمر بعد أي تغيير مستقبلي لملفات `assets/icon/`.

### الخطوة 0.4 — تجربة سريعة على هاتف/محاكي
```bash
flutter run
```
- في شاشة الإعداد أدخل عنوان خادمك مثل: `http://192.168.1.10:5005`
- جرّب الدخول برقم موظف أصدرت له رقمًا سريًا من الداشبورد (شاشة الموظفين ← زر 🔑).

---

# المرحلة 1: أندرويد — APK موقَّع لا ترفضه الهواتف

سبب رفض التثبيت أو رفض **التحديثات** لاحقًا هو التوقيع بمفتاح debug المؤقت.
سنوقّع بمفتاح release ثابت.

### الخطوة 1.1 — إنشاء مفتاح التوقيع (مرة واحدة في العمر)
```bash
keytool -genkey -v -keystore ~/attendance-portal.jks \
  -keyalg RSA -keysize 2048 -validity 10000 -alias portal
```
سيسألك:
1. **كلمة مرور المخزن** — اخترها واحفظها (سنسميها هنا `MyStorePass123`).
2. الاسم/المنظمة/المدينة/الدولة — اكتب ما يناسب (مثلًا: Ministry / IT / SA).
3. تأكيد بـ `yes`.
4. كلمة مرور المفتاح — اضغط Enter لاستخدام نفس كلمة المخزن.

> 🔐 **احتفظ بالملف `~/attendance-portal.jks` وكلمة المرور للأبد** (انسخه لمكان آمن).
> فقدانه = استحالة تحديث التطبيق على أجهزة الموظفين (سيحتاجون حذف وإعادة تثبيت).

### الخطوة 1.2 — ملف كلمات المرور `key.properties`
أنشئ الملف:
```bash
nano android/key.properties
```
والصق (عدّل كلمة المرور واسم المستخدم في المسار):
```properties
storePassword=MyStorePass123
keyPassword=MyStorePass123
keyAlias=portal
storeFile=/Users/it/attendance-portal.jks
```
احفظ بـ `Ctrl+O` ثم Enter ثم `Ctrl+X`.
> هذا الملف مستبعد من git تلقائيًا (في `.gitignore`) — لن تُرفع أسرارك.

### الخطوة 1.3 — ربط التوقيع في إعداد البناء
اعرف أي صيغة ولّدها Flutter لديك:
```bash
ls android/app/ | grep build.gradle
```

#### الحالة أ: الملف `build.gradle.kts` (مشاريع Flutter الحديثة)
افتح `android/app/build.gradle.kts` وأضف **أعلى الملف** (بعد كتلة `plugins { }`):
```kotlin
import java.util.Properties
import java.io.FileInputStream

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}
```
ثم **داخل كتلة `android { ... }`** أضف كتلة `signingConfigs` وعدّل `buildTypes` الموجودة:
```kotlin
    signingConfigs {
        create("release") {
            keyAlias = keystoreProperties["keyAlias"] as String
            keyPassword = keystoreProperties["keyPassword"] as String
            storeFile = file(keystoreProperties["storeFile"] as String)
            storePassword = keystoreProperties["storePassword"] as String
        }
    }

    buildTypes {
        release {
            // كان: signingConfig = signingConfigs.getByName("debug")
            signingConfig = signingConfigs.getByName("release")
        }
    }
```

#### الحالة ب: الملف `build.gradle` (صيغة Groovy الأقدم)
افتح `android/app/build.gradle` وأضف **قبل** سطر `android {`:
```groovy
def keystoreProperties = new Properties()
def keystorePropertiesFile = rootProject.file('key.properties')
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(new FileInputStream(keystorePropertiesFile))
}
```
ثم داخل `android { ... }`:
```groovy
    signingConfigs {
        release {
            keyAlias keystoreProperties['keyAlias']
            keyPassword keystoreProperties['keyPassword']
            storeFile file(keystoreProperties['storeFile'])
            storePassword keystoreProperties['storePassword']
        }
    }
    buildTypes {
        release {
            // كان: signingConfig signingConfigs.debug
            signingConfig signingConfigs.release
        }
    }
```

### الخطوة 1.4 — بناء الـ APK
```bash
flutter build apk --release
```
نتيجة متوقعة في آخر السطور:
```
✓ Built build/app/outputs/flutter-apk/app-release.apk (XX.X MB)
```

### الخطوة 1.5 — التحقق من التوقيع (اختياري للاطمئنان)
```bash
keytool -printcert -jarfile build/app/outputs/flutter-apk/app-release.apk
```
يجب أن ترى بيانات شهادتك (Ministry/IT...) وليس "Android Debug".

### الخطوة 1.6 — التوزيع على الموظفين
- أرسل `app-release.apk` عبر مشاركة داخلية (إيميل/مجلد شبكة/USB).
- على هاتف الموظف: فتح الملف ← سيطلب "السماح بالتثبيت من مصادر غير معروفة" ← سماح ←
  إن ظهر تنبيه Play Protect "تطبيق غير معروف" ← **تثبيت على أي حال** — تنبيه طبيعي
  لأي تطبيق خارج المتجر، وليس رفضًا.

### الخطوة 1.7 — إصدار تحديث مستقبلًا
1. في `pubspec.yaml` ارفع السطر: `version: 1.0.0+1` ← `version: 1.0.1+2`
   (الرقم بعد `+` يجب أن يزيد في كل إصدار).
2. `flutter build apk --release` بنفس المفتاح.
3. وزّع — سيُثبَّت **فوق** النسخة القديمة دون حذف بيانات الدخول.

---

# المرحلة 2: iOS — الإصدار على TestFlight

### الخطوة 2.0 — حساب Apple Developer (مرة واحدة)
- سجّل في [developer.apple.com/programs/enroll](https://developer.apple.com/programs/enroll)
  (99$/سنة). بدونه لا يوجد TestFlight.
- بعد التفعيل، ادخل بحسابك على [appstoreconnect.apple.com](https://appstoreconnect.apple.com)
  للتأكد أنه يعمل.

### الخطوة 2.1 — تعديل Info.plist (إلزامي — بدونه لن يتصل التطبيق بخادمك)
افتح:
```bash
open ios/Runner/Info.plist
```
أضف **داخل `<dict>` الجذر** (قبل سطر `</dict>` الأخير):
```xml
	<!-- السماح بالاتصال بخادم HTTP على الشبكة الداخلية (iOS يحجب http افتراضيًا) -->
	<key>NSAppTransportSecurity</key>
	<dict>
		<key>NSAllowsArbitraryLoads</key>
		<true/>
		<key>NSAllowsLocalNetworking</key>
		<true/>
	</dict>
	<!-- إذن الوصول للشبكة المحلية (يظهر للموظف مرة واحدة في iOS 14+) -->
	<key>NSLocalNetworkUsageDescription</key>
	<string>يتصل التطبيق بخادم الوزارة على الشبكة الداخلية لعرض بيانات حضورك.</string>
	<!-- اسم التطبيق تحت الأيقونة -->
	<key>CFBundleDisplayName</key>
	<string>حضوري</string>
	<!-- الدخول ببصمة الوجه (Face ID) -->
	<key>NSFaceIDUsageDescription</key>
	<string>يستخدم التطبيق بصمة الوجه لتسجيل دخولك بسرعة وأمان.</string>
```

### الخطوة 2.2 — التوقيع في Xcode (مرة واحدة)
```bash
open ios/Runner.xcworkspace
```
> ⚠️ افتح `Runner.xcworkspace` وليس `Runner.xcodeproj`.

داخل Xcode:
1. القائمة **Xcode ← Settings ← Accounts** ← زر ➕ ← **Apple ID** ← سجّل دخول حساب المطوّر.
2. في الشريط الجانبي الأيسر اضغط أعلى عنصر (**Runner** بأيقونة زرقاء).
3. في الوسط اختر **TARGETS ← Runner** ← تبويب **Signing & Capabilities**.
4. علّم ✅ **Automatically manage signing**.
5. **Team**: اختر فريق حسابك.
6. **Bundle Identifier**: اتركه `sa.gov.ministry.attendancePortal` (أو عدّله لمعرّف
   فريد ثم **لا تغيّره أبدًا**).
7. انتظر ثوانيَ حتى تختفي أي أخطاء حمراء تحت الحقول (Xcode يسجّل المعرّف تلقائيًا).

جرّب على محاكي iOS للاطمئنان:
```bash
open -a Simulator
flutter run
```

### الخطوة 2.3 — إنشاء التطبيق في App Store Connect (مرة واحدة)
في [appstoreconnect.apple.com](https://appstoreconnect.apple.com):
1. **My Apps** ← زر ➕ ← **New App**.
2. املأ:
   - **Platforms**: iOS
   - **Name**: حضوري (أو "حضوري — وزارة …" إن كان الاسم محجوزًا)
   - **Primary Language**: Arabic
   - **Bundle ID**: اختر من القائمة نفس المعرّف الذي ظهر في Xcode
   - **SKU**: أي نص فريد مثل `hodhoori-2026`
   - **User Access**: Full Access
3. **Create**.

### الخطوة 2.4 — بناء ملف الرفع (ipa)
```bash
flutter build ipa --release
```
نتيجة متوقعة:
```
✓ Built build/ios/ipa/attendance_portal.ipa
```
> إن فشل بخطأ توقيع: ارجع للخطوة 2.2 وتأكد من اختيار الـ Team، ثم أعد الأمر.

### الخطوة 2.5 — رفع الملف إلى Apple
**الطريقة الأسهل — تطبيق Transporter:**
1. ثبّت **Transporter** من Mac App Store (مجاني، من Apple).
2. افتحه وسجّل بنفس Apple ID.
3. اسحب الملف `build/ios/ipa/attendance_portal.ipa` إلى نافذته ← **Deliver**.
4. انتظر "Delivery Successful".

بعد 5–30 دقيقة يظهر البناء في App Store Connect ← تطبيقك ← تبويب **TestFlight**
(ستصلك رسالة "processing completed").
> إن ظهر بجانب البناء تحذير أصفر **Missing Compliance**: اضغطه ← سؤال التشفير ←
> اختر **None of the algorithms mentioned above / Standard encryption** ← احفظ.
> (التطبيق يستخدم https/http القياسي فقط.)

### الخطوة 2.6 — إضافة إيميلات الموظفين (المطلوب الأساسي)
في App Store Connect ← تطبيقك ← **TestFlight**:

**أ) مجموعة خارجية للموظفين (حتى 10,000):**
1. من الشريط الجانبي: **External Testing** ← زر ➕ ← اسم المجموعة: `الموظفون` ← **Create**.
2. داخل المجموعة ← قسم **Builds** ← ➕ ← اختر البناء الذي رفعته ← **Next**.
3. سيطلب **Test Information** (مرة واحدة):
   - **Beta App Description**: تطبيق داخلي لموظفي الوزارة لمتابعة الحضور والانصراف.
   - **Feedback Email**: إيميلك.
   - **What to Test**: تسجيل الدخول وعرض بيانات الحضور.
4. في **Review Notes** الصق هذا النص الإنجليزي (مهم جدًا حتى لا يتعثر الاعتماد لأن
   المراجع خارج شبكتكم لا يستطيع الدخول):
   ```
   Internal ministry employee-attendance viewer. Login requires the ministry's
   private network and employee credentials issued by the admin dashboard;
   no public accounts exist. The app only displays the employee's own
   fingerprint-device attendance records. UI can be evaluated from the
   attached screenshots.
   ```
5. **Submit for Review** ← مراجعة البيتا تستغرق عادة يومًا أو يومين.
6. بعد الاعتماد: داخل المجموعة ← **Testers** ← ➕ ← **Add New Testers**:
   - أدخل الإيميلات يدويًا (إيميل + اسم أول + اسم أخير لكل موظف)، **أو**
   - **Import from CSV** لرفع ملف دفعة واحدة بصيغة:
     ```csv
     first_name,last_name,email
     أحمد,محمد,ahmed@example.com
     سارة,علي,sara@example.com
     ```
7. يصل كل موظف **إيميل دعوة** من Apple:
   - يثبّت تطبيق **TestFlight** من App Store.
   - يفتح الدعوة من الإيميل ← **View in TestFlight** ← **Install**.

**ب) (اختياري) تجربتك أنت فورًا بلا مراجعة — Internal Testing:**
1. **Internal Testing** ← ➕ مجموعة ← أضف نفسك (يجب أن يكون إيميلك مستخدمًا في
   الفريق عبر **Users and Access**).
2. البناء يتاح لك فورًا دون أي مراجعة — مناسب لتجربتك قبل تعميم المجموعة الخارجية.

### مرجع: محتوى نموذج Test Information جاهز للصق
- **Beta App Description (عربي)**: وصف التطبيق للموظفين — انظر النص الكامل في قسم
  "محتوى TestFlight الجاهز" أدناه.
- **Feedback Email**: إيميل المسؤول.
- **Marketing URL / Privacy Policy URL / License Agreement**: تُترك فارغة في TestFlight.
- **Sign-in required**: يُترك **بدون تحديد** — ويُشرح السبب في Review Notes (الدخول
  مستحيل خارج الشبكة الداخلية وبربط جهاز واحد)، فطلب حساب تجريبي سيفشل حتمًا.
- **Review Notes (إنجليزي)**:
  ```
  "Hodhoori" is an internal employee-attendance viewer for a government ministry.

  IMPORTANT — why sign-in cannot be demonstrated:
  - The app connects ONLY to the ministry's on-premises server over the private
    internal network (private IP ranges). Outside that network the app
    intentionally shows a clear message: "You are outside the ministry network".
  - Sign-in requires an employee number and a secret PIN issued by the ministry's
    admin dashboard, and each account is hard-bound to a single physical device
    after its first login. Therefore no working demo account can function on a
    reviewer's device or network by design.

  What the reviewer can verify without credentials:
  - The server-setup screen, the login screen, and the offline/out-of-network
    message are all reachable immediately.
  - All post-login screens (Today status, monthly summary, punch records,
    account) are shown in the App Screenshots.

  Scope: read-only display of the signed-in employee's own attendance data.
  No public registration, no payments, no user-generated content, no ads,
  no tracking, standard HTTPS/HTTP networking only.
  ```
- **الوصف العربي الكامل (Beta App Description)**:
  ```
  تطبيق «حضوري» هو تطبيق داخلي مخصص لموظفي الوزارة لمتابعة الحضور والانصراف.

  يتيح التطبيق للموظف الاطلاع على:
  • حالة اليوم: أول بصمة دخول، آخر بصمة خروج، ودقائق التأخير إن وجدت.
  • ملخص الشهر: أيام الحضور والغياب، مرات ودقائق التأخير، ساعات العمل، ونسبة الحضور.
  • سجل البصمات يومًا بيوم لأي شهر.
  • بيانات حسابه وورديته المعتمدة.

  يتصل التطبيق حصريًا بخادم الوزارة عبر الشبكة الداخلية، ويتطلب الدخول رقم الموظف
  ورقمًا سريًا تصدره إدارة النظام، مع ربط الحساب بجهاز واحد لحماية البيانات.
  البيانات للعرض فقط وتُحدَّث تلقائيًا كل 15 دقيقة وفق جدول المزامنة مع جهاز البصمة.
  ```

### الخطوة 2.7 — تحديثات iOS مستقبلًا
1. ارفع `version` في `pubspec.yaml` (مثل `1.0.1+2`).
2. `flutter build ipa --release` ← رفع عبر Transporter.
3. TestFlight ← أضف البناء الجديد للمجموعة (التحديثات التالية غالبًا **لا تحتاج
   مراجعة جديدة** أو تُعتمد أسرع) — يصل الموظفين إشعار تحديث في تطبيق TestFlight.
4. ⏳ تذكير: كل بناء صالح **90 يومًا** — أصدِر بناءً جديدًا قبل انتهائها.

---

# حل المشكلات الشائعة

| المشكلة | الحل |
|---|---|
| أندرويد: "App not installed" عند التثبيت | تأكد أنك وزّعت `app-release.apk` (وليس debug)، وأن الخطوة 1.3 طُبّقت ثم أعد البناء |
| أندرويد: التحديث يرفض التثبيت فوق القديم | اختلف التوقيع — استخدم نفس ملف `.jks` دائمًا، وارفع رقم `+` في version |
| `flutter build apk` يفشل بـ "key.properties" | تأكد من مسار `storeFile` الكامل الصحيح في key.properties وكلمات المرور |
| iOS: التطبيق لا يتصل بالخادم | لم تُطبَّق الخطوة 2.1 (ATS في Info.plist) — طبّقها وأعد البناء |
| iOS: ظهر سؤال "الشبكة المحلية" ورفضه الموظف | إعدادات iOS ← حضوري ← تفعيل "الشبكة المحلية" |
| `flutter build ipa` يفشل: signing | Xcode ← Runner ← Signing: اختر Team وفعّل Automatic، ثم أعد الأمر |
| البناء لا يظهر في TestFlight بعد الرفع | انتظر حتى 30 دقيقة، وراجع إيميلك — قد يكون هناك رفض تلقائي بسبب أيقونة ناقصة مثلًا |
| رفض Beta Review لعدم القدرة على الدخول | ردّ عليهم بنص الـ Review Notes أعلاه مع لقطات شاشة من التطبيق |
| دعوة TestFlight لم تصل موظفًا | تحقق من صندوق Spam، أو احذفه وأعد إضافته، وتأكد أن الإيميل صحيح |

---

# ملخص الأوامر (بعد الإعداد الأول)

```bash
# أندرويد — إصدار جديد
cd mobile
flutter build apk --release
# → build/app/outputs/flutter-apk/app-release.apk

# iOS — إصدار جديد
flutter build ipa --release
# → build/ios/ipa/*.ipa ← ارفعه بـ Transporter ← TestFlight
```
