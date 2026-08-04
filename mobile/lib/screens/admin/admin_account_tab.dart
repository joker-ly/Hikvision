import 'package:flutter/material.dart';

import '../../api.dart';
import '../../main.dart';
import '../guide.dart';
import '../login.dart';

/// حساب المدير: بياناته وتعليمات ودعم وتسجيل الخروج.
class AdminAccountTab extends StatelessWidget {
  const AdminAccountTab({super.key});

  Future<void> _logout(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text('هل تريد تسجيل الخروج من لوحة المدير؟'),
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
    if (context.mounted) goTo(context, const LoginScreen());
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
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
                backgroundColor: Colors.white.withValues(alpha: .2),
                child: const Icon(Icons.admin_panel_settings,
                    size: 38, color: Colors.white),
              ),
              const SizedBox(height: 10),
              Text(Api.fullName,
                  style: const TextStyle(
                      color: Colors.white,
                      fontSize: 19,
                      fontWeight: FontWeight.bold)),
              const SizedBox(height: 4),
              Text('حساب إداري — عرض التقارير والإحصائيات',
                  style: TextStyle(
                      color: Colors.white.withValues(alpha: .85), fontSize: 12.5)),
            ],
          ),
        ),
        const SizedBox(height: 14),
        Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor: Colors.teal.withValues(alpha: .12),
              child: const Icon(Icons.sync, color: Colors.teal, size: 20),
            ),
            title: const Text('تحديث البيانات',
                style: TextStyle(fontSize: 12, color: Colors.grey)),
            subtitle: Text(
              'تُزامَن البصمات كل ${Api.syncInfo.intervalMinutes} دقيقة '
              '(${Api.syncInfo.windowLabel})؛ الأرقام تعكس آخر مزامنة.',
              style: const TextStyle(fontSize: 13.5, color: Colors.black87),
            ),
          ),
        ),
        Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: ListTile(
            onTap: () => Navigator.of(context).push(MaterialPageRoute(
                builder: (_) => const GuideScreen(asHelp: true))),
            leading: CircleAvatar(
              backgroundColor: const Color(0xFF1E3A8A).withValues(alpha: .1),
              child: const Icon(Icons.help_outline,
                  color: Color(0xFF1E3A8A), size: 20),
            ),
            title: const Text('تعليمات الاستخدام',
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
            subtitle: const Text('آلية عمل التطبيق وشرح النوافذ',
                style: TextStyle(fontSize: 12)),
            trailing: const Icon(Icons.chevron_left, color: Colors.grey),
          ),
        ),
        Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor: Colors.orange.withValues(alpha: .12),
              child:
                  const Icon(Icons.support_agent, color: Colors.orange, size: 20),
            ),
            title: const Text('مكتب تقنية المعلومات',
                style: TextStyle(fontSize: 12, color: Colors.grey)),
            subtitle: Text(Api.supportPhone,
                textDirection: TextDirection.ltr,
                style: const TextStyle(fontSize: 14, color: Colors.black87)),
          ),
        ),
        const SizedBox(height: 8),
        FilledButton.icon(
          style: FilledButton.styleFrom(backgroundColor: Colors.red.shade600),
          onPressed: () => _logout(context),
          icon: const Icon(Icons.logout),
          label: const Text('تسجيل الخروج'),
        ),
      ],
    );
  }
}
