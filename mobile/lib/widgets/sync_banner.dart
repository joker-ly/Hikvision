import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';

/// ملاحظة بارزة: المزامنة كل X دقيقة ضمن نافذة الدوام + مؤقّت تنازلي للمزامنة القادمة.
class SyncBanner extends StatefulWidget {
  const SyncBanner({super.key});

  @override
  State<SyncBanner> createState() => _SyncBannerState();
}

class _SyncBannerState extends State<SyncBanner> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    // تحديث الإعدادات من الخادم ثم عدّاد يحدّث كل ثانية
    Api.refreshSyncInfo().then((_) {
      if (mounted) setState(() {});
    });
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final info = Api.syncInfo;
    final now = DateTime.now();
    final next = info.nextSync(now);
    final inWindow =
        !now.isBefore(info.startOf(now)) && now.isBefore(info.endOf(now));
    final remaining = next.difference(now);

    String countdown;
    if (remaining.inHours >= 1) {
      countdown =
          '${remaining.inHours}:${(remaining.inMinutes % 60).toString().padLeft(2, '0')}:${(remaining.inSeconds % 60).toString().padLeft(2, '0')}';
    } else {
      countdown =
          '${remaining.inMinutes.toString().padLeft(2, '0')}:${(remaining.inSeconds % 60).toString().padLeft(2, '0')}';
    }

    final nextLabel = inWindow || now.isBefore(info.startOf(now))
        ? 'المزامنة القادمة خلال'
        : 'انتهت مزامنات اليوم — القادمة غدًا خلال';

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [Colors.amber.shade50, Colors.orange.shade50],
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.amber.shade300),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.sync, color: Colors.orange.shade800, size: 22),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'المزامنة كل ${info.intervalMinutes} دقيقة (${info.windowLabel})',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                    color: Colors.orange.shade900,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'البيانات الجديدة لا تظهر إلا بعد اكتمال المزامنة مع جهاز البصمة.',
            style: TextStyle(fontSize: 13, color: Colors.orange.shade900),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(Icons.timer_outlined,
                  size: 18, color: Colors.orange.shade800),
              const SizedBox(width: 6),
              Text(nextLabel,
                  style:
                      TextStyle(fontSize: 13, color: Colors.orange.shade900)),
              const SizedBox(width: 8),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.orange.shade800,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  countdown,
                  textDirection: TextDirection.ltr,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                    fontFeatures: [FontFeature.tabularFigures()],
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
