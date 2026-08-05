import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import 'guide.dart';

class LoginScreen extends StatefulWidget {
  /// عند true تُطلب البصمة تلقائيًا فور فتح الشاشة (إن كانت مفعّلة).
  final bool autoBiometric;
  const LoginScreen({super.key, this.autoBiometric = true});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _noController = TextEditingController();
  final _pinController = TextEditingController();
  bool _busy = false;
  bool _remember = true;
  bool _canBiometric = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _prepare();
  }

  Future<void> _prepare() async {
    // تعبئة رقم الموظف المحفوظ
    final savedNo = await Api.savedEmployeeNo();
    if (savedNo != null) _noController.text = savedNo;

    // تفعيل زر البصمة إن توفرت بيانات محفوظة ودعم للجهاز
    final creds = await Api.savedCredentials();
    final supported = await Api.biometricsAvailable();
    if (mounted) {
      setState(() => _canBiometric =
          creds != null && supported && Api.biometricEnabled);
    }

    // طلب البصمة تلقائيًا عند فتح الشاشة
    if (_canBiometric && widget.autoBiometric) {
      await _biometricLogin();
    }
  }

  Future<void> _afterLoginOfferBiometric() async {
    // اقتراح تفعيل الدخول بالبصمة بعد أول دخول ناجح ببيانات محفوظة
    if (Api.biometricEnabled) return;
    if (!await Api.biometricsAvailable()) return;
    if (!mounted) return;

    final enable = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('الدخول بالبصمة'),
        content: const Text(
            'هل تريد تفعيل الدخول ببصمة الإصبع/الوجه بدل إدخال البيانات في كل مرة؟'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('لاحقًا')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('تفعيل')),
        ],
      ),
    );
    if (enable == true) await Api.setBiometricEnabled(true);
  }

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
      if (_remember) {
        await Api.saveCredentials(no, pin);
        await _afterLoginOfferBiometric();
      } else {
        await Api.clearCredentials();
      }
      if (mounted) goTo(context, homeForRole());
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = Api.offlineMessage);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _biometricLogin() async {
    setState(() => _error = null);
    final ok = await Api.biometricAuthenticate();
    if (!ok) return;

    setState(() => _busy = true);
    final err = await Api.loginWithSaved();
    if (!mounted) return;
    setState(() => _busy = false);
    if (err == null) {
      goTo(context, homeForRole());
    } else {
      setState(() {
        _error = err;
        _canBiometric = false;
      });
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

                // زر الدخول بالبصمة مباشرة
                if (_canBiometric) ...[
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: _busy ? null : _biometricLogin,
                      icon: const Icon(Icons.fingerprint, size: 26),
                      label: const Text('الدخول بالبصمة'),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Row(children: [
                    const Expanded(child: Divider()),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      child: Text('أو أدخل بياناتك',
                          style: TextStyle(
                              color: Colors.grey.shade600, fontSize: 12)),
                    ),
                    const Expanded(child: Divider()),
                  ]),
                  const SizedBox(height: 16),
                ],

                TextField(
                  controller: _noController,
                  keyboardType: TextInputType.text,
                  decoration: const InputDecoration(
                    labelText: 'رقم الموظف أو اسم المستخدم',
                    prefixIcon: Icon(Icons.badge),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _pinController,
                  // لوحة نصية: الموظف يدخل أرقامًا والمدير كلمة مرور فيها حروف
                  keyboardType: TextInputType.visiblePassword,
                  obscureText: true,
                  autocorrect: false,
                  enableSuggestions: false,
                  decoration: const InputDecoration(
                    labelText: 'الرقم السري أو كلمة المرور',
                    prefixIcon: Icon(Icons.password),
                  ),
                  onSubmitted: (_) => _login(),
                ),
                SwitchListTile(
                  value: _remember,
                  onChanged: (v) => setState(() => _remember = v),
                  title: const Text('حفظ بيانات الدخول',
                      style: TextStyle(fontSize: 14)),
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                ),
                if (_error != null) ...[
                  Text(_error!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.red)),
                  const SizedBox(height: 8),
                ],
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
                const SizedBox(height: 10),
                TextButton.icon(
                  onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                      builder: (_) => const GuideScreen(asHelp: true))),
                  icon: const Icon(Icons.help_outline, size: 18),
                  label: const Text('تعليمات الاستخدام'),
                ),
                Text(
                  'للحصول على الرقم السري تواصل مع مكتب تقنية المعلومات\n${Api.supportPhone}',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 12.5, color: Colors.grey.shade600),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
