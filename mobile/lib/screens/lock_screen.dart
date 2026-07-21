import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import 'home_shell.dart';
import 'login.dart';

/// قفل بالبصمة عند فتح التطبيق والجلسة سارية — حماية بيانات الموظف.
class LockScreen extends StatefulWidget {
  const LockScreen({super.key});

  @override
  State<LockScreen> createState() => _LockScreenState();
}

class _LockScreenState extends State<LockScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _unlock());
  }

  Future<void> _unlock() async {
    final ok = await Api.biometricAuthenticate();
    if (!mounted) return;
    if (ok) goTo(context, const HomeShell());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.fingerprint, size: 90, color: Color(0xFF1E3A8A)),
              const SizedBox(height: 16),
              Text('حضوري', style: Theme.of(context).textTheme.headlineMedium),
              const SizedBox(height: 6),
              const Text('استخدم بصمتك للمتابعة',
                  style: TextStyle(color: Colors.grey)),
              const SizedBox(height: 28),
              FilledButton.icon(
                onPressed: _unlock,
                icon: const Icon(Icons.fingerprint),
                label: const Text('فتح بالبصمة'),
              ),
              const SizedBox(height: 10),
              TextButton(
                onPressed: () =>
                    goTo(context, const LoginScreen(autoBiometric: false)),
                child: const Text('الدخول بالرقم السري بدلًا من ذلك'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
