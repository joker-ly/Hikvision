import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import 'home_shell.dart';
import 'server_setup.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _noController = TextEditingController();
  final _pinController = TextEditingController();
  bool _busy = false;
  String? _error;

  Future<void> _login() async {
    final no = _noController.text.trim();
    final pin = _pinController.text.trim();
    if (no.isEmpty || pin.isEmpty) {
      setState(() => _error = 'أدخل رقم الموظف والرقم السري.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await Api.login(no, pin);
      if (mounted) goTo(context, const HomeShell());
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'تعذّر الاتصال بالخادم. تأكد أنك على شبكة الوزارة.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                const Icon(Icons.fingerprint, size: 72, color: Color(0xFF1E3A8A)),
                const SizedBox(height: 12),
                Text('حضوري', style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 4),
                const Text('متابعة الحضور والانصراف',
                    style: TextStyle(color: Colors.grey)),
                const SizedBox(height: 24),
                TextField(
                  controller: _noController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'رقم الموظف (رقم البصمة)',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.badge),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _pinController,
                  keyboardType: TextInputType.number,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'الرقم السري',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.password),
                  ),
                  onSubmitted: (_) => _login(),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(_error!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.red)),
                ],
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    onPressed: _busy ? null : _login,
                    icon: _busy
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.login),
                    label: const Text('دخول'),
                  ),
                ),
                const SizedBox(height: 12),
                TextButton.icon(
                  onPressed: () => goTo(context, const ServerSetupScreen()),
                  icon: const Icon(Icons.dns, size: 18),
                  label: const Text('تغيير عنوان الخادم'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
