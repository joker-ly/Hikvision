import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import '../widgets/sync_banner.dart';

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

          // الحالة الرئيسية لبطاقة البطل
          final (heroColor, heroIcon, heroTitle, heroSub) = !isWorkingDay
              ? (Colors.blueGrey, Icons.weekend, 'يوم عطلة', 'اليوم ليس يوم عمل وفق ورديتك')
              : firstIn == null
                  ? (Colors.grey, Icons.hourglass_empty, 'لم تُسجَّل بصمة بعد', 'بانتظار بصمة الدخول أو المزامنة القادمة')
                  : late > 0
                      ? (Colors.orange, Icons.alarm, 'حضرت متأخرًا', 'تأخّرت $late دقيقة عن موعد الحضور')
                      : (Colors.green, Icons.verified, 'حضور في الموعد', 'أحسنت! التزمت بموعد الدوام');

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const SyncBanner(),
              // بطاقة الحالة الرئيسية
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topRight,
                    end: Alignment.bottomLeft,
                    colors: [heroColor.shade600, heroColor.shade400],
                  ),
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                      color: heroColor.withOpacity(.3),
                      blurRadius: 12,
                      offset: const Offset(0, 6),
                    ),
                  ],
                ),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 28,
                      backgroundColor: Colors.white.withOpacity(.25),
                      child: Icon(heroIcon, color: Colors.white, size: 30),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(heroTitle,
                              style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 19,
                                  fontWeight: FontWeight.bold)),
                          const SizedBox(height: 4),
                          Text(heroSub,
                              style: TextStyle(
                                  color: Colors.white.withOpacity(.9),
                                  fontSize: 13)),
                          const SizedBox(height: 6),
                          Text('اليوم ${d['date']}',
                              style: TextStyle(
                                  color: Colors.white.withOpacity(.75),
                                  fontSize: 12)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              // بطاقتا الدخول والخروج جنبًا إلى جنب
              Row(
                children: [
                  Expanded(
                    child: _TimeCard(
                      icon: Icons.login,
                      label: 'أول دخول',
                      time: firstIn,
                      color: Colors.green,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _TimeCard(
                      icon: Icons.logout,
                      label: 'آخر خروج',
                      time: lastOut,
                      color: Colors.teal,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Center(
                child: Text(
                  'اسحب الشاشة للأسفل لتحديث البيانات',
                  style: TextStyle(color: Colors.grey.shade500, fontSize: 12),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _TimeCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String? time;
  final MaterialColor color;

  const _TimeCard(
      {required this.icon,
      required this.label,
      required this.time,
      required this.color});

  @override
  Widget build(BuildContext context) {
    final has = time != null;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.blueGrey.shade50),
      ),
      child: Column(
        children: [
          CircleAvatar(
            radius: 20,
            backgroundColor: (has ? color : Colors.grey).withOpacity(.12),
            child: Icon(icon, color: has ? color : Colors.grey, size: 22),
          ),
          const SizedBox(height: 8),
          Text(label,
              style: TextStyle(fontSize: 12, color: Colors.grey.shade600)),
          const SizedBox(height: 4),
          Text(
            time ?? '—',
            textDirection: TextDirection.ltr,
            style: TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.bold,
              color: has ? Colors.black87 : Colors.grey,
            ),
          ),
        ],
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
