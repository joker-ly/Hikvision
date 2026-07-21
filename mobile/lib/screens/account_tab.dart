import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import '../widgets/sync_banner.dart';
import 'login.dart';

class AccountTab extends StatefulWidget {
  const AccountTab({super.key});

  @override
  State<AccountTab> createState() => _AccountTabState();
}

class _AccountTabState extends State<AccountTab> {
  late Future<Map<String, dynamic>> _future;

  @override
  void initState() {
    super.initState();
    _future = Api.me();
  }

  Future<void> _logout() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد تسجيل الخروج من التطبيق؟'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('خروج')),
        ],
      ),
    );
    if (ok != true) return;
    await Api.logout();
    if (mounted) goTo(context, const LoginScreen());
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Map<String, dynamic>>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          handleAuthError(context, snap.error!);
          return Center(child: Text(snap.error.toString()));
        }
        final d = snap.data!;
        String s(String k) => d[k]?.toString() ?? '—';

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // بطاقة الملف الشخصي
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  begin: Alignment.topRight,
                  end: Alignment.bottomLeft,
                  colors: [Color(0xFF1E3A8A), Color(0xFF3B5BC0)],
                ),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Column(
                children: [
                  CircleAvatar(
                    radius: 34,
                    backgroundColor: Colors.white.withOpacity(.2),
                    child: const Icon(Icons.person, size: 38, color: Colors.white),
                  ),
                  const SizedBox(height: 10),
                  Text(s('fullName'),
                      style: const TextStyle(
                          color: Colors.white,
                          fontSize: 19,
                          fontWeight: FontWeight.bold)),
                  const SizedBox(height: 4),
                  Text('${s('groupName')} · رقم ${s('employeeNo')}',
                      style: TextStyle(
                          color: Colors.white.withOpacity(.85), fontSize: 13)),
                ],
              ),
            ),
            const SizedBox(height: 14),
            const SyncBanner(),
            _InfoTile(Icons.account_balance, 'الرقم المالي', s('financialNo')),
            _InfoTile(
                Icons.schedule,
                'الدوام',
                d['scheduleStart'] != null
                    ? '${s('scheduleStart')} — ${s('scheduleEnd')} (سماح ${s('lateGraceMinutes')} د)'
                    : 'غير مقيّد بمواعيد'),
            _InfoTile(Icons.phone_android, 'هذا الجهاز',
                '${d['deviceInfo'] ?? '—'}\nمرتبط منذ ${s('deviceBoundAt')}'),
            Card(
              margin: const EdgeInsets.only(bottom: 10),
              child: SwitchListTile(
                value: Api.biometricEnabled,
                onChanged: (v) async {
                  if (v) {
                    final creds = await Api.savedCredentials();
                    if (creds == null) {
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                            content: Text(
                                'فعّل "حفظ بيانات الدخول" عند تسجيل الدخول أولًا.')));
                      }
                      return;
                    }
                    if (!await Api.biometricsAvailable()) {
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                            content: Text('جهازك لا يدعم البصمة/الوجه.')));
                      }
                      return;
                    }
                  }
                  await Api.setBiometricEnabled(v);
                  if (mounted) setState(() {});
                },
                secondary: const CircleAvatar(
                  backgroundColor: Color(0x1A1E3A8A),
                  child: Icon(Icons.fingerprint, color: Color(0xFF1E3A8A)),
                ),
                title: const Text('الدخول بالبصمة',
                    style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                subtitle: const Text('فتح التطبيق ببصمة الإصبع/الوجه بدل إدخال البيانات',
                    style: TextStyle(fontSize: 12)),
              ),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              style: FilledButton.styleFrom(backgroundColor: Colors.red.shade600),
              onPressed: _logout,
              icon: const Icon(Icons.logout),
              label: const Text('تسجيل الخروج'),
            ),
          ],
        );
      },
    );
  }
}

class _InfoTile extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;
  const _InfoTile(this.icon, this.label, this.value);

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: const Color(0xFF1E3A8A).withOpacity(.1),
          child: Icon(icon, color: const Color(0xFF1E3A8A), size: 20),
        ),
        title: Text(label,
            style: const TextStyle(fontSize: 12, color: Colors.grey)),
        subtitle: Text(value,
            style: const TextStyle(fontSize: 15, color: Colors.black87)),
      ),
    );
  }
}
