import 'package:flutter/material.dart';

import 'api.dart';
import 'screens/home_shell.dart';
import 'screens/login.dart';
import 'screens/server_setup.dart';

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
    if (Api.serverUrl == null) {
      start = const ServerSetupScreen();
    } else if (Api.token == null) {
      start = const LoginScreen();
    } else {
      start = const HomeShell();
    }

    return MaterialApp(
      title: 'حضوري',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF1E3A8A)),
        useMaterial3: true,
        fontFamily: 'Roboto',
      ),
      builder: (context, child) =>
          Directionality(textDirection: TextDirection.rtl, child: child!),
      home: start,
    );
  }
}

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
