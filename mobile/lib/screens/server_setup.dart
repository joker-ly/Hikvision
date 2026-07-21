import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import 'login.dart';

/// شاشة أول تشغيل: إدخال عنوان خادم الوزارة الداخلي.
class ServerSetupScreen extends StatefulWidget {
  const ServerSetupScreen({super.key});

  @override
  State<ServerSetupScreen> createState() => _ServerSetupScreenState();
}

class _ServerSetupScreenState extends State<ServerSetupScreen> {
  final _controller = TextEditingController(text: Api.serverUrl ?? 'http://192.168.');
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    final url = _controller.text.trim();
    if (url.isEmpty || !url.startsWith('http')) {
      setState(() => _error = 'أدخل عنوانًا صحيحًا يبدأ بـ http مثل http://192.168.1.10:5005');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final ok = await Api.ping(url);
      if (!ok) throw Exception();
      await Api.setServer(url);
      if (mounted) goTo(context, const LoginScreen());
    } catch (_) {
      setState(() => _error =
          'تعذّر الوصول للخادم. تأكد أنك على شبكة الوزارة وأن العنوان صحيح.');
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
                const Text('إعداد الاتصال بخادم الوزارة',
                    style: TextStyle(color: Colors.grey)),
                const SizedBox(height: 24),
                TextField(
                  controller: _controller,
                  keyboardType: TextInputType.url,
                  textDirection: TextDirection.ltr,
                  decoration: const InputDecoration(
                    labelText: 'عنوان الخادم',
                    hintText: 'http://192.168.1.10:5005',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.dns),
                  ),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(_error!, style: const TextStyle(color: Colors.red)),
                ],
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    onPressed: _busy ? null : _save,
                    icon: _busy
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.check),
                    label: const Text('اختبار ومتابعة'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
