import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
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
            const CircleAvatar(
              radius: 36,
              backgroundColor: Color(0xFF1E3A8A),
              child: Icon(Icons.person, size: 40, color: Colors.white),
            ),
            const SizedBox(height: 8),
            Center(
                child: Text(s('fullName'),
                    style: Theme.of(context).textTheme.titleLarge)),
            const SizedBox(height: 16),
            _InfoTile(Icons.badge, 'رقم الموظف', s('employeeNo')),
            _InfoTile(Icons.account_balance, 'الرقم المالي', s('financialNo')),
            _InfoTile(Icons.group, 'المجموعة', s('groupName')),
            _InfoTile(Icons.schedule, 'الدوام',
                d['scheduleStart'] != null
                    ? '${s('scheduleStart')} — ${s('scheduleEnd')} (سماح ${s('lateGraceMinutes')} د)'
                    : 'غير مقيّد بمواعيد'),
            _InfoTile(Icons.phone_android, 'هذا الجهاز',
                '${d['deviceInfo'] ?? '—'}\nمرتبط منذ ${s('deviceBoundAt')}'),
            const SizedBox(height: 20),
            FilledButton.icon(
              style: FilledButton.styleFrom(backgroundColor: Colors.red),
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
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: Icon(icon, color: const Color(0xFF1E3A8A)),
        title: Text(label,
            style: const TextStyle(fontSize: 12, color: Colors.grey)),
        subtitle: Text(value,
            style: const TextStyle(fontSize: 15, color: Colors.black87)),
      ),
    );
  }
}
