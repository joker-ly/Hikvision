import 'package:flutter/material.dart';

import 'api.dart';
import 'screens/admin/admin_shell.dart';
import 'screens/guide.dart';
import 'screens/home_shell.dart';
import 'screens/lock_screen.dart';
import 'screens/login.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Api.init();
  runApp(const PortalApp());
}

class PortalApp extends StatelessWidget {
  const PortalApp({super.key});

  @override
  Widget build(BuildContext context) {
    final Widget start;
    if (!Api.guideSeen) {
      // أول تشغيل: شاشة التعليمات
      start = const GuideScreen();
    } else if (Api.token == null) {
      start = const LoginScreen();
    } else if (Api.biometricEnabled) {
      // جلسة سارية + بصمة مفعّلة → قفل بالبصمة قبل الدخول
      start = const LockScreen();
    } else {
      start = homeForRole();
    }

    const brand = Color(0xFF1E3A8A);
    return MaterialApp(
      title: 'حضوري',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: brand),
        useMaterial3: true,
        scaffoldBackgroundColor: const Color(0xFFF4F6FB),
        appBarTheme: const AppBarTheme(
          backgroundColor: brand,
          foregroundColor: Colors.white,
          centerTitle: true,
          elevation: 0,
        ),
        cardTheme: CardThemeData(
          elevation: 0,
          color: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: BorderSide(color: Colors.blueGrey.shade50),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: BorderSide(color: Colors.blueGrey.shade100),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: BorderSide(color: Colors.blueGrey.shade100),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: brand, width: 1.6),
          ),
        ),
        filledButtonTheme: FilledButtonThemeData(
          style: FilledButton.styleFrom(
            minimumSize: const Size.fromHeight(50),
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14)),
            textStyle:
                const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
          ),
        ),
        navigationBarTheme: NavigationBarThemeData(
          backgroundColor: Colors.white,
          indicatorColor: brand.withValues(alpha: .12),
          labelTextStyle: WidgetStatePropertyAll(
            TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
        ),
      ),
      builder: (context, child) =>
          Directionality(textDirection: TextDirection.rtl, child: child!),
      home: start,
    );
  }
}

/// الواجهة الرئيسية حسب دور الحساب: لوحة المدير أو شاشات الموظف.
Widget homeForRole() => Api.isAdmin ? const AdminShell() : const HomeShell();

/// انتقال موحّد مع تفريغ سجل الشاشات (تسجيل خروج/دخول).
void goTo(BuildContext context, Widget screen) {
  Navigator.of(context).pushAndRemoveUntil(
    MaterialPageRoute(builder: (_) => screen),
    (route) => false,
  );
}

/// معالجة 401: تنظيف الجلسة والعودة لتسجيل الدخول.
Future<void> handleAuthError(BuildContext context, Object error) async {
  if (error is ApiException && error.statusCode == 401) {
    await Api.clearSession();
    if (context.mounted) goTo(context, const LoginScreen());
  }
}
