import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import '../widgets/error_view.dart';

class MonthTab extends StatefulWidget {
  const MonthTab({super.key});

  @override
  State<MonthTab> createState() => _MonthTabState();
}

class _MonthTabState extends State<MonthTab> {
  late int _year;
  late int _month;
  late Future<Map<String, dynamic>> _future;

  static const _monthNames = [
    'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
    'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'
  ];

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _future = Api.summary(_year, _month);
  }

  void _shift(int delta) {
    var m = _month + delta;
    var y = _year;
    if (m < 1) {
      m = 12;
      y--;
    } else if (m > 12) {
      m = 1;
      y++;
    }
    setState(() {
      _month = m;
      _year = y;
      _future = Api.summary(_year, _month);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: Row(
            children: [
              IconButton(
                  onPressed: () => _shift(-1),
                  icon: const Icon(Icons.chevron_right)),
              Expanded(
                child: Text('${_monthNames[_month - 1]} $_year',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.titleMedium),
              ),
              IconButton(
                  onPressed: () => _shift(1),
                  icon: const Icon(Icons.chevron_left)),
            ],
          ),
        ),
        Expanded(
          child: FutureBuilder<Map<String, dynamic>>(
            future: _future,
            builder: (context, snap) {
              if (snap.connectionState == ConnectionState.waiting) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snap.hasError) {
                handleAuthError(context, snap.error!);
                return ConnectionErrorView(
                  error: snap.error!,
                  onRetry: () async {
                    setState(() {
                      _future = Api.summary(_year, _month);
                    });
                  },
                );
              }
              final d = snap.data!;
              if ((d['periodDays'] as num?)?.toInt() == 0) {
                return const Center(child: Text('لا بيانات لهذا الشهر بعد.'));
              }

              int n(String k) => (d[k] as num?)?.toInt() ?? 0;
              final rate = (d['presenceRate'] as num?)?.toDouble() ?? 0;
              final rateColor = rate >= 90
                  ? Colors.green
                  : rate >= 75
                      ? Colors.orange
                      : Colors.red;

              final rateHeader = Container(
                margin: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.blueGrey.shade50),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Text('نسبة الحضور',
                            style: TextStyle(
                                fontSize: 14, fontWeight: FontWeight.w600)),
                        const Spacer(),
                        Text('$rate%',
                            style: TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                                color: rateColor)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: LinearProgressIndicator(
                        value: (rate / 100).clamp(0.0, 1.0),
                        minHeight: 10,
                        backgroundColor: Colors.blueGrey.shade50,
                        valueColor: AlwaysStoppedAnimation(rateColor),
                      ),
                    ),
                  ],
                ),
              );

              final grid = GridView.count(
                padding: const EdgeInsets.all(16),
                crossAxisCount: 2,
                mainAxisSpacing: 10,
                crossAxisSpacing: 10,
                childAspectRatio: 1.5,
                children: [
                  _Tile('أيام الحضور', '${n('presentDays')} / ${n('periodDays')}',
                      Colors.green, Icons.check_circle),
                  _Tile('أيام الغياب', '${n('absentDays')}',
                      n('absentDays') > 0 ? Colors.red : Colors.grey, Icons.cancel),
                  _Tile('مرات التأخير', '${n('lateCount')}',
                      n('lateCount') > 0 ? Colors.orange : Colors.grey, Icons.alarm),
                  _Tile('دقائق التأخير', '${n('lateMinutes')}',
                      n('lateMinutes') > 0 ? Colors.orange : Colors.grey, Icons.timer),
                  _Tile('ساعات العمل', '${d['workedHours'] ?? 0}',
                      Colors.blue, Icons.schedule),
                  _Tile('إجازات', '${n('leaveDays')}', Colors.teal, Icons.beach_access),
                  _Tile('مهام عمل', '${n('missionDays')}', Colors.indigo, Icons.work),
                  _Tile('إجازة بدون مرتب', '${n('unpaidLeaveDays')}',
                      n('unpaidLeaveDays') > 0 ? Colors.deepOrange : Colors.grey,
                      Icons.money_off),
                ],
              );

              return Column(
                children: [rateHeader, Expanded(child: grid)],
              );
            },
          ),
        ),
      ],
    );
  }
}

class _Tile extends StatelessWidget {
  final String label;
  final String value;
  final Color color;
  final IconData icon;
  const _Tile(this.label, this.value, this.color, this.icon);

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Row(children: [
              Icon(icon, color: color, size: 20),
              const SizedBox(width: 6),
              Expanded(
                  child: Text(label,
                      style: const TextStyle(fontSize: 13, color: Colors.grey))),
            ]),
            const SizedBox(height: 6),
            Text(value,
                style: TextStyle(
                    fontSize: 22, fontWeight: FontWeight.bold, color: color)),
          ],
        ),
      ),
    );
  }
}
