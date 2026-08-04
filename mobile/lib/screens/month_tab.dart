import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import '../widgets/error_view.dart';
import '../widgets/month_picker.dart';

class MonthTab extends StatefulWidget {
  const MonthTab({super.key});

  @override
  State<MonthTab> createState() => _MonthTabState();
}

class _MonthTabState extends State<MonthTab> {
  late int _year;
  late int _month;
  late Future<Map<String, dynamic>> _future;

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
        MonthPicker(year: _year, month: _month, onShift: _shift),
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
              final rateHeader = Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                child: PresenceRateBar(rate: rate),
              );

              final grid = GridView.count(
                padding: const EdgeInsets.all(16),
                crossAxisCount: 2,
                mainAxisSpacing: 10,
                crossAxisSpacing: 10,
                childAspectRatio: 1.5,
                children: [
                  StatTile('أيام الحضور', '${n('presentDays')} / ${n('periodDays')}',
                      Colors.green, Icons.check_circle),
                  StatTile('أيام الغياب', '${n('absentDays')}',
                      n('absentDays') > 0 ? Colors.red : Colors.grey, Icons.cancel),
                  StatTile('مرات التأخير', '${n('lateCount')}',
                      n('lateCount') > 0 ? Colors.orange : Colors.grey, Icons.alarm),
                  StatTile('دقائق التأخير', '${n('lateMinutes')}',
                      n('lateMinutes') > 0 ? Colors.orange : Colors.grey, Icons.timer),
                  StatTile('ساعات العمل', '${d['workedHours'] ?? 0}',
                      Colors.blue, Icons.schedule),
                  StatTile('إجازات', '${n('leaveDays')}', Colors.teal, Icons.beach_access),
                  StatTile('مهام عمل', '${n('missionDays')}', Colors.indigo, Icons.work),
                  StatTile('إجازة بدون مرتب', '${n('unpaidLeaveDays')}',
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
