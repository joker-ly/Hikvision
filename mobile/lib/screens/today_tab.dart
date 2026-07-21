import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';

class TodayTab extends StatefulWidget {
  const TodayTab({super.key});

  @override
  State<TodayTab> createState() => _TodayTabState();
}

class _TodayTabState extends State<TodayTab> {
  late Future<Map<String, dynamic>> _future;

  @override
  void initState() {
    super.initState();
    _future = Api.today();
  }

  Future<void> _refresh() async {
    setState(() => _future = Api.today());
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _refresh,
      child: FutureBuilder<Map<String, dynamic>>(
        future: _future,
        builder: (context, snap) {
          if (snap.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            handleAuthError(context, snap.error!);
            return _ErrorView(message: snap.error.toString(), onRetry: _refresh);
          }
          final d = snap.data!;
          final firstIn = d['firstIn'] as String?;
          final lastOut = d['lastOut'] as String?;
          final late = (d['lateMinutes'] as num?)?.toInt() ?? 0;
          final isWorkingDay = d['isWorkingDay'] == true;

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text('اليوم ${d['date']}',
                  style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 12),
              if (!isWorkingDay)
                const _StatusCard(
                    icon: Icons.weekend,
                    color: Colors.blueGrey,
                    title: 'يوم عطلة',
                    subtitle: 'اليوم ليس يوم عمل وفق ورديتك'),
              _StatusCard(
                icon: Icons.login,
                color: firstIn != null ? Colors.green : Colors.grey,
                title: 'أول دخول',
                subtitle: firstIn ?? 'لم تُسجَّل بصمة دخول بعد',
              ),
              _StatusCard(
                icon: Icons.logout,
                color: lastOut != null ? Colors.teal : Colors.grey,
                title: 'آخر خروج',
                subtitle: lastOut ?? '—',
              ),
              _StatusCard(
                icon: late > 0 ? Icons.alarm : Icons.check_circle,
                color: late > 0 ? Colors.orange : Colors.green,
                title: late > 0 ? 'متأخر' : 'الالتزام بالموعد',
                subtitle: late > 0 ? 'تأخّرت $late دقيقة عن موعد الحضور' : 'لا تأخير اليوم',
              ),
              const SizedBox(height: 8),
              const Text(
                'البيانات بحسب آخر مزامنة مع جهاز البصمة — اسحب للأسفل للتحديث.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.grey, fontSize: 12),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;

  const _StatusCard(
      {required this.icon,
      required this.color,
      required this.title,
      required this.subtitle});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: color.withOpacity(.15),
          child: Icon(icon, color: color),
        ),
        title: Text(title),
        subtitle: Text(subtitle, style: const TextStyle(fontSize: 16)),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  final String message;
  final Future<void> Function() onRetry;
  const _ErrorView({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        const SizedBox(height: 40),
        const Icon(Icons.wifi_off, size: 56, color: Colors.grey),
        const SizedBox(height: 12),
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        Center(
          child: FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة'),
          ),
        ),
      ],
    );
  }
}
