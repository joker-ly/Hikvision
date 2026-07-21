# دليل الإصدار والتوزيع — أندرويد (APK) و iOS (TestFlight)

> المتطلبات جاهزة لديك: ماك + Xcode + Flutter + Android Studio.
> نفّذ أولًا "الإعداد الأول للمشروع" في `README.md` (flutter create + تعديل AndroidManifest).

---

## أولًا: أندرويد — APK موقَّع لا ترفضه الهواتف

سبب رفض/تحذير الهواتف الشائع هو **التوقيع بمفتاح debug** (توقيع مؤقت). الحل: توقيع
release بمفتاح ثابت خاص بك — وهو أيضًا **شرط لتحديث التطبيق لاحقًا** على نفس الأجهزة
(التحديث يُرفض إن اختلف التوقيع).

### 1) إنشاء مفتاح التوقيع (مرة واحدة — احتفظ بالملف وكلمة المرور للأبد)
```bash
keytool -genkey -v -keystore ~/attendance-portal.jks \
  -keyalg RSA -keysize 2048 -validity 10000 -alias portal
```

### 2) ملف `mobile/android/key.properties` (لا يُرفع للمستودع)
```properties
storePassword=كلمة-مرور-المخزن
keyPassword=كلمة-مرور-المفتاح
keyAlias=portal
storeFile=/Users/اسمك/attendance-portal.jks
```

### 3) ربط التوقيع في `mobile/android/app/build.gradle.kts`
أعلى الملف (بعد سطور plugins):
```kotlin
import java.util.Properties
import java.io.FileInputStream

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}
```
وداخل `android { ... }`:
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
            signingConfig = signingConfigs.getByName("release")
        }
    }
```
> إن كان مشروعك ولّد `build.gradle` (Groovy) بدل `.kts` فالصيغة تختلف قليلًا — أخبرني وأعطيك مقابلها.

### 4) البناء
```bash
cd mobile
flutter build apk --release
```
الناتج: `build/app/outputs/flutter-apk/app-release.apk` — **موقَّع وجاهز للتوزيع**.

### لماذا لن ترفضه الهواتف الآن؟
- ✅ موقَّع بمفتاح release ثابت (لا "توقيع غير صالح").
- ✅ APK شامل لكل المعالجات (arm64/arm32) افتراضيًا — لا "التطبيق غير متوافق".
- ✅ نفس المفتاح = التحديثات المستقبلية تُقبل فوق النسخة القديمة.
- ⚠️ يبقى تنبيهان طبيعيان لأي تطبيق خارج المتجر (ليسا رفضًا):
  1. "السماح بالتثبيت من مصادر غير معروفة" — يفعّله الموظف مرة واحدة.
  2. قد يعرض Play Protect "تطبيق غير معروف — هل تريد التثبيت؟" → "تثبيت على أي حال".
- 🚫 لا تغيّر `applicationId` ولا المفتاح بين الإصدارات، ولكل إصدار جديد ارفع
  `version` في `pubspec.yaml` (مثل `1.0.1+2`).

---

## ثانيًا: iOS — الإصدار على TestFlight

### 0) متطلب حساب
اشتراك **Apple Developer Program** (99$/سنة) بحساب المنشأة أو حسابك — لا يمكن
التوزيع عبر TestFlight بدونه.

### 1) تجهيزات iOS في المشروع (مرة واحدة)
افتح `mobile/ios/Runner/Info.plist` وأضف داخل `<dict>` الجذر:
```xml
	<!-- السماح بالاتصال بخادم HTTP على الشبكة الداخلية (iOS يحجب http افتراضيًا) -->
	<key>NSAppTransportSecurity</key>
	<dict>
		<key>NSAllowsArbitraryLoads</key>
		<true/>
		<key>NSAllowsLocalNetworking</key>
		<true/>
	</dict>
	<!-- إذن الوصول للشبكة المحلية (iOS 14+) -->
	<key>NSLocalNetworkUsageDescription</key>
	<string>يتصل التطبيق بخادم الوزارة على الشبكة الداخلية لعرض بيانات حضورك.</string>
	<!-- اسم التطبيق تحت الأيقونة -->
	<key>CFBundleDisplayName</key>
	<string>حضوري</string>
```
> بدون قسم `NSAppTransportSecurity` سيفشل الاتصال بخادمك الداخلي على iOS نهائيًا.

### 2) التوقيع في Xcode (مرة واحدة)
```bash
cd mobile
open ios/Runner.xcworkspace
```
في Xcode: هدف **Runner** ← تبويب **Signing & Capabilities**:
- سجّل الدخول بحساب المطوّر (Xcode ← Settings ← Accounts).
- فعّل **Automatically manage signing** واختر الـ Team.
- تأكد من **Bundle Identifier** فريد (مثل `sa.gov.ministry.attendancePortal`) —
  سيُسجَّل تلقائيًا في حسابك.

### 3) إنشاء التطبيق في App Store Connect (مرة واحدة)
[appstoreconnect.apple.com](https://appstoreconnect.apple.com) ← **My Apps** ← ➕ ←
**New App**: المنصة iOS، الاسم "حضوري"، اللغة العربية، اختر الـ Bundle ID نفسه، وأي SKU.

### 4) البناء والرفع
```bash
flutter build ipa --release
```
ثم ارفع الملف الناتج `build/ios/ipa/*.ipa` بإحدى طريقتين:
- تطبيق **Transporter** من Mac App Store (اسحب الـ ipa وارفع) — الأسهل، أو
- `xcrun altool` / نافذة Organizer في Xcode.

بعد الرفع بدقائق يظهر البناء في App Store Connect ← تبويب **TestFlight**
(قد يطلب إقرار Export Compliance: أجب "لا يستخدم تشفيرًا غير قياسي").

### 5) إضافة إيميلات الموظفين
في **TestFlight**:
1. أنشئ مجموعة اختبار خارجية: **External Testing** ← ➕ مجموعة باسم "الموظفون".
2. اربط البناء (Build) بالمجموعة.
3. **Add Testers ← Add New Testers** ← أدخل إيميلات الموظفين (يدويًا أو استيراد CSV) —
   حتى 10,000 مختبِر.
4. أول بناء لمجموعة خارجية يمر بمراجعة **Beta App Review** (يوم–يومان عادة).
   في **Review Notes** اكتب بالإنجليزية ما يشرح أنه تطبيق داخلي:
   > Internal ministry employee-attendance viewer. Requires the ministry's private
   > network to log in; no public accounts exist. UI can be reviewed from screenshots.
5. بعد الاعتماد يصل لكل موظف **إيميل دعوة**: يثبّت تطبيق TestFlight من App Store،
   يفتح الدعوة، فيُثبَّت "حضوري".

> بديل بلا مراجعة: **Internal Testing** — فوري لكنه يتطلب إضافة كل شخص كمستخدم في
> فريق App Store Connect (حد 100) — مناسب لتجربتك أنت قبل تعميم المجموعة الخارجية.

### ملاحظات TestFlight
- كل بناء صالح **90 يومًا** — ارفع بناءً جديدًا قبل الانتهاء (نفس الخطوات، مع رفع
  `version` في `pubspec.yaml`).
- ربط الجهاز الواحد يعمل على iOS تلقائيًا (نفس آلية معرّف الجهاز في التطبيق).
- إن رُفض البناء في مراجعة البيتا بسبب تعذّر الدخول، ردّ عليهم بالتوضيح أعلاه أو
  زوّدهم بلقطات شاشة — تطبيقات المنشآت الداخلية تُقبل عادة بهذه الملاحظات.
