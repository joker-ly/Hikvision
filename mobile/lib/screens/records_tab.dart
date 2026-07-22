import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import '../widgets/error_view.dart';

class RecordsTab extends StatefulWidget {
  const RecordsTab({super.key});

  @override
  State<RecordsTab> createState() => _RecordsTabState();
}

class _RecordsTabState extends State<RecordsTab> {
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
    _future = Api.records(_year, _month);
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
      _future = Api.records(_year, _month);
    });
  }

  String _dirText(String d) => switch (d) {
        'CheckIn' => 'دخول',
        'CheckOut' => 'خروج',
        'BreakIn' => 'نهاية استراحة',
        'BreakOut' => 'بداية استراحة',
        _ => 'بصمة',
      };

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
                      _future = Api.records(_year, _month);
                    });
                  },
                );
              }
              final records = (snap.data!['records'] as List).cast<Map<String, dynamic>>();
              if (records.isEmpty) {
                return const Center(child: Text('لا سجلات في هذا الشهر.'));
              }

              // تجميع حسب التاريخ
              final byDate = <String, List<Map<String, dynamic>>>{};
              for (final r in records) {
                byDate.putIfAbsent(r['date'] as String, () => []).add(r);
              }
              final dates = byDate.keys.toList();

              return ListView.builder(
                padding: const EdgeInsets.all(12),
                itemCount: dates.length,
                itemBuilder: (context, i) {
                  final date = dates[i];
                  final list = byDate[date]!;
                  return Card(
                    margin: const EdgeInsets.only(bottom: 10),
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(date,
                              style: const TextStyle(fontWeight: FontWeight.bold)),
                          const Divider(),
                          ...list.reversed.map((r) {
                            final isManual = r['source'] == 'Manual';
                            final dir = r['direction'] as String;
                            final isIn = dir == 'CheckIn';
                            final isOut = dir == 'CheckOut';
                            final (icon, color) = isIn
                                ? (Icons.login, Colors.green)
                                : isOut
                                    ? (Icons.logout, Colors.teal)
                                    : (Icons.fingerprint, Colors.blueGrey);
                            return Padding(
                              padding: const EdgeInsets.symmetric(vertical: 3),
                              child: Row(
                                children: [
                                  Icon(icon, size: 18, color: color),
                                  const SizedBox(width: 8),
                                  Text(r['time'] as String,
                                      style: const TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w500)),
                                  const SizedBox(width: 8),
                                  Text(_dirText(r['direction'] as String)),
                                  const Spacer(),
                                  if (isManual)
                                    const Chip(
                                      label: Text('يدوي',
                                          style: TextStyle(fontSize: 11)),
                                      visualDensity: VisualDensity.compact,
                                    ),
                                ],
                              ),
                            );
                          }),
                        ],
                      ),
                    ),
                  );
                },
              );
            },
          ),
        ),
      ],
    );
  }
}
