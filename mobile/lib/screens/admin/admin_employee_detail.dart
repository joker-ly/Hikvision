import 'package:flutter/material.dart';

import '../../api.dart';
import '../../main.dart';
import '../../widgets/error_view.dart';
import '../../widgets/month_picker.dart';

/// إحصائيات موظف واحد لشهر قابل للتنقّل + آخر بصماته.
class AdminEmployeeDetail extends StatefulWidget {
  final int employeeId;
  final String fullName;

  const AdminEmployeeDetail({
    super.key,
    required this.employeeId,
    required this.fullName,
  });

  @override
  State<AdminEmployeeDetail> createState() => _AdminEmployeeDetailState();
}

class _AdminEmployeeDetailState extends State<AdminEmployeeDetail> {
  late int _year;
  late int _month;
  late Future<Map<String, dynamic>> _future;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _load();
  }

  void _load() => setState(() {
        _future = Api.employeeStats(widget.employeeId, _year, _month);
      });

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
    _year = y;
    _month = m;
    _load();
  }

  int _n(Map<String, dynamic> d, String k) => (d[k] as num?)?.toInt() ?? 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.fullName)),
      body: Column(
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
                    onRetry: () async => _load(),
                  );
                }
                final d = snap.data!;
                final emp = d['employee'] as Map<String, dynamic>;
                final periodDays = _n(d, 'periodDays');
                final rate = (d['presenceRate'] as num?)?.toDouble() ?? 0;
                final records =
                    (d['records'] as List).cast<Map<String, dynamic>>();

                return ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    // بطاقة بيانات الموظف
                    Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          begin: Alignment.topRight,
                          end: Alignment.bottomLeft,
                          colors: [Color(0xFF1E3A8A), Color(0xFF3B5BC0)],
                        ),
                        borderRadius: BorderRadius.circular(18),
                      ),
                      child: Row(
                        children: [
                          CircleAvatar(
                            radius: 26,
                            backgroundColor:
                                Colors.white.withValues(alpha: .2),
                            child: const Icon(Icons.person,
                                size: 30, color: Colors.white),
                          ),
                          const SizedBox(width: 14),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(emp['fullName'] as String,
                                    style: const TextStyle(
                                        color: Colors.white,
                                        fontSize: 17,
                                        fontWeight: FontWeight.bold)),
                                const SizedBox(height: 4),
                                Text(
                                  [
                                    if (emp['groupName'] != null)
                                      emp['groupName'] as String,
                                    'بصمة ${emp['employeeNo']}',
                                    if (emp['financialNo'] != null)
                                      'مالي ${emp['financialNo']}',
                                  ].join(' · '),
                                  style: TextStyle(
                                      color: Colors.white
                                          .withValues(alpha: .85),
                                      fontSize: 12),
                                ),
                                if (emp['scheduleStart'] != null)
                                  Text(
                                    'الدوام ${emp['scheduleStart']} — ${emp['scheduleEnd']}',
                                    style: TextStyle(
                                        color: Colors.white
                                            .withValues(alpha: .75),
                                        fontSize: 11.5),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 14),

                    if (emp['isExempt'] == true)
                      _Banner(
                        icon: Icons.verified_user,
                        color: Colors.blueGrey,
                        text: 'مجموعة معفاة من البصمة — الحضور محتسب تلقائيًا.',
                      ),
                    if (emp['exemptionDate'] != null)
                      _Banner(
                        icon: Icons.event_busy,
                        color: Colors.deepOrange,
                        text:
                            'معفى من ${emp['exemptionDate']} — لا يُحتسب حضور بعده.',
                      ),

                    if (periodDays == 0)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 32),
                        child: Center(
                          child: Text('لا بيانات لهذا الشهر.',
                              style: TextStyle(color: Colors.grey)),
                        ),
                      )
                    else ...[
                      PresenceRateBar(rate: rate),
                      const SizedBox(height: 12),
                      GridView.count(
                        shrinkWrap: true,
                        physics: const NeverScrollableScrollPhysics(),
                        crossAxisCount: 2,
                        mainAxisSpacing: 10,
                        crossAxisSpacing: 10,
                        childAspectRatio: 1.5,
                        children: [
                          StatTile('أيام الحضور',
                              '${_n(d, 'presentDays')} / $periodDays',
                              Colors.green, Icons.check_circle),
                          StatTile('أيام الغياب', '${_n(d, 'absentDays')}',
                              _n(d, 'absentDays') > 0 ? Colors.red : Colors.grey,
                              Icons.cancel),
                          StatTile('مرات التأخير', '${_n(d, 'lateCount')}',
                              _n(d, 'lateCount') > 0
                                  ? Colors.orange
                                  : Colors.grey,
                              Icons.alarm),
                          StatTile('دقائق التأخير', '${_n(d, 'lateMinutes')}',
                              _n(d, 'lateMinutes') > 0
                                  ? Colors.orange
                                  : Colors.grey,
                              Icons.timer),
                          StatTile('ساعات العمل', '${d['workedHours'] ?? 0}',
                              Colors.blue, Icons.schedule),
                          StatTile('إجازات', '${_n(d, 'leaveDays')}',
                              Colors.teal, Icons.beach_access),
                          StatTile('مهام عمل', '${_n(d, 'missionDays')}',
                              Colors.indigo, Icons.work),
                          StatTile('أذونات خروج', '${_n(d, 'permittedExits')}',
                              Colors.blueGrey, Icons.door_front_door),
                        ],
                      ),
                      const SizedBox(height: 16),
                      if (records.isNotEmpty) _RecordsCard(records: records),
                    ],
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String text;
  const _Banner({required this.icon, required this.color, required this.text});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: .1),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(text,
                style: TextStyle(fontSize: 13, color: color)),
          ),
        ],
      ),
    );
  }
}

class _RecordsCard extends StatelessWidget {
  final List<Map<String, dynamic>> records;
  const _RecordsCard({required this.records});

  String _dirText(String d) => switch (d) {
        'CheckIn' => 'دخول',
        'CheckOut' => 'خروج',
        'BreakIn' => 'نهاية استراحة',
        'BreakOut' => 'بداية استراحة',
        _ => 'بصمة',
      };

  @override
  Widget build(BuildContext context) {
    // تجميع حسب التاريخ
    final byDate = <String, List<Map<String, dynamic>>>{};
    for (final r in records) {
      byDate.putIfAbsent(r['date'] as String, () => []).add(r);
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(children: [
              Icon(Icons.list_alt, color: Color(0xFF1E3A8A), size: 20),
              SizedBox(width: 8),
              Text('آخر البصمات',
                  style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold)),
            ]),
            const Divider(),
            ...byDate.entries.map((entry) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(entry.key,
                          style: const TextStyle(
                              fontSize: 13, fontWeight: FontWeight.bold)),
                      ...entry.value.map((r) {
                        final dir = r['direction'] as String;
                        final isIn = dir == 'CheckIn';
                        final isOut = dir == 'CheckOut';
                        final (icon, color) = isIn
                            ? (Icons.login, Colors.green)
                            : isOut
                                ? (Icons.logout, Colors.teal)
                                : (Icons.fingerprint, Colors.blueGrey);
                        return Padding(
                          padding: const EdgeInsets.symmetric(vertical: 2),
                          child: Row(
                            children: [
                              Icon(icon, size: 16, color: color),
                              const SizedBox(width: 8),
                              Text(r['time'] as String,
                                  style: const TextStyle(
                                      fontSize: 14,
                                      fontWeight: FontWeight.w500)),
                              const SizedBox(width: 8),
                              Text(_dirText(dir),
                                  style: const TextStyle(fontSize: 13)),
                              const Spacer(),
                              if (r['source'] == 'Manual')
                                const Text('يدوي',
                                    style: TextStyle(
                                        fontSize: 11, color: Colors.grey)),
                            ],
                          ),
                        );
                      }),
                    ],
                  ),
                )),
          ],
        ),
      ),
    );
  }
}
