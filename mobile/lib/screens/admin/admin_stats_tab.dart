import 'package:flutter/material.dart';

import '../../api.dart';
import '../../main.dart';
import '../../widgets/error_view.dart';
import '../../widgets/month_picker.dart';
import 'admin_employee_detail.dart';

/// المؤشرات العامة وقوائم الأعلى وملخص المجموعات.
class AdminStatsTab extends StatefulWidget {
  const AdminStatsTab({super.key});

  @override
  State<AdminStatsTab> createState() => _AdminStatsTabState();
}

class _AdminStatsTabState extends State<AdminStatsTab> {
  late int _year;
  late int _month;
  int? _groupId;
  List<Map<String, dynamic>> _groups = const [];
  late Future<Map<String, dynamic>> _future;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _year = now.year;
    _month = now.month;
    _future = Api.adminStats(_year, _month);
    _loadGroups();
  }

  Future<void> _loadGroups() async {
    try {
      final d = await Api.adminGroups();
      if (mounted) {
        setState(() => _groups =
            (d['groups'] as List).cast<Map<String, dynamic>>());
      }
    } catch (_) {
      // فلتر المجموعات إضافي — تجاهل فشله
    }
  }

  void _reload() => setState(() {
        _future = Api.adminStats(_year, _month, groupId: _groupId);
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
    _reload();
  }

  int _n(Map<String, dynamic> d, String k) => (d[k] as num?)?.toInt() ?? 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        MonthPicker(year: _year, month: _month, onShift: _shift),
        if (_groups.isNotEmpty)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: Colors.blueGrey.shade100),
              ),
              child: Row(
                children: [
                  const Icon(Icons.account_tree,
                      size: 20, color: Color(0xFF1E3A8A)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: DropdownButton<int?>(
                      value: _groupId,
                      isExpanded: true,
                      underline: const SizedBox.shrink(),
                      items: [
                        const DropdownMenuItem<int?>(
                            value: null, child: Text('كل المجموعات')),
                        ..._groups.map((g) => DropdownMenuItem<int?>(
                            value: g['id'] as int,
                            child: Text(g['name'] as String))),
                      ],
                      onChanged: (v) {
                        _groupId = v;
                        _reload();
                      },
                    ),
                  ),
                ],
              ),
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
                  onRetry: () async => _reload(),
                );
              }
              final d = snap.data!;
              if (d['empty'] == true) {
                return const Center(child: Text('لا بيانات لهذا الشهر بعد.'));
              }

              final k = d['kpis'] as Map<String, dynamic>;
              final avgRate = (k['avgPresenceRate'] as num?)?.toDouble() ?? 0;

              return RefreshIndicator(
                onRefresh: () async => _reload(),
                child: ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    PresenceRateBar(
                        rate: avgRate, label: 'متوسط نسبة الحضور العام'),
                    const SizedBox(height: 12),
                    GridView.count(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      crossAxisCount: 2,
                      mainAxisSpacing: 10,
                      crossAxisSpacing: 10,
                      childAspectRatio: 1.5,
                      children: [
                        StatTile('عدد الموظفين', '${_n(k, 'employeeCount')}',
                            Colors.indigo, Icons.people),
                        StatTile('إجمالي الغياب', '${_n(k, 'totalAbsentDays')}',
                            _n(k, 'totalAbsentDays') > 0 ? Colors.red : Colors.grey,
                            Icons.cancel),
                        StatTile('مرات التأخير', '${_n(k, 'totalLateCount')}',
                            Colors.orange, Icons.alarm),
                        StatTile('دقائق التأخير', '${_n(k, 'totalLateMinutes')}',
                            Colors.orange, Icons.timer),
                        StatTile('إجازات', '${_n(k, 'totalLeaveDays')}',
                            Colors.teal, Icons.beach_access),
                        StatTile('بدون مرتب', '${_n(k, 'totalUnpaidLeaveDays')}',
                            Colors.deepOrange, Icons.money_off),
                        StatTile('مهام عمل', '${_n(k, 'totalMissionDays')}',
                            Colors.indigo, Icons.work),
                        StatTile('أذونات خروج', '${_n(k, 'totalPermittedExits')}',
                            Colors.blueGrey, Icons.door_front_door),
                      ],
                    ),
                    const SizedBox(height: 16),
                    _TopList(
                      title: 'الأكثر غيابًا',
                      icon: Icons.trending_down,
                      color: Colors.red,
                      rows: (d['topAbsent'] as List).cast<Map<String, dynamic>>(),
                      valueOf: (e) => '${e['daysAbsent']} يوم · ${e['absenceRate']}%',
                    ),
                    _TopList(
                      title: 'الأكثر تأخرًا',
                      icon: Icons.alarm,
                      color: Colors.orange,
                      rows: (d['topLate'] as List).cast<Map<String, dynamic>>(),
                      valueOf: (e) => '${e['lateCount']} مرة · ${e['totalLateMinutes']} د',
                    ),
                    _TopList(
                      title: 'الأكثر انضباطًا',
                      icon: Icons.emoji_events,
                      color: Colors.green,
                      rows: (d['topPresent'] as List).cast<Map<String, dynamic>>(),
                      valueOf: (e) => '${e['daysPresent']} يوم · ${e['presenceRate']}%',
                    ),
                    _GroupsCard(
                        rows: (d['groups'] as List).cast<Map<String, dynamic>>()),
                  ],
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _TopList extends StatelessWidget {
  final String title;
  final IconData icon;
  final Color color;
  final List<Map<String, dynamic>> rows;
  final String Function(Map<String, dynamic>) valueOf;

  const _TopList({
    required this.title,
    required this.icon,
    required this.color,
    required this.rows,
    required this.valueOf,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Icon(icon, color: color, size: 20),
              const SizedBox(width: 8),
              Text(title,
                  style: const TextStyle(
                      fontSize: 15, fontWeight: FontWeight.bold)),
            ]),
            const Divider(),
            if (rows.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text('لا يوجد', style: TextStyle(color: Colors.grey)),
              ),
            ...rows.asMap().entries.map((entry) {
              final i = entry.key;
              final e = entry.value;
              return ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                onTap: () => Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => AdminEmployeeDetail(
                    employeeId: e['employeeId'] as int,
                    fullName: e['fullName'] as String,
                  ),
                )),
                leading: CircleAvatar(
                  radius: 14,
                  backgroundColor: color.withValues(alpha: .12),
                  child: Text('${i + 1}',
                      style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: color)),
                ),
                title: Text(e['fullName'] as String,
                    style: const TextStyle(fontSize: 14)),
                subtitle: Text(e['groupName'] as String? ?? '',
                    style: const TextStyle(fontSize: 11)),
                trailing: Text(valueOf(e),
                    style: TextStyle(
                        fontSize: 12.5,
                        fontWeight: FontWeight.w600,
                        color: color)),
              );
            }),
          ],
        ),
      ),
    );
  }
}

class _GroupsCard extends StatelessWidget {
  final List<Map<String, dynamic>> rows;
  const _GroupsCard({required this.rows});

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) return const SizedBox.shrink();
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(children: [
              Icon(Icons.account_tree, color: Color(0xFF1E3A8A), size: 20),
              SizedBox(width: 8),
              Text('ملخص المجموعات',
                  style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold)),
            ]),
            const Divider(),
            ...rows.map((g) {
              final rate = (g['avgPresenceRate'] as num?)?.toDouble() ?? 0;
              return Padding(
                padding: const EdgeInsets.symmetric(vertical: 6),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(g['groupName'] as String,
                              style: const TextStyle(
                                  fontSize: 14, fontWeight: FontWeight.w600)),
                          Text(
                            '${g['employeeCount']} موظف · غياب ${g['totalAbsentDays']} · تأخير ${g['totalLateCount']}',
                            style: const TextStyle(
                                fontSize: 11.5, color: Colors.grey),
                          ),
                        ],
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: PresenceRateBar.colorFor(rate)
                            .withValues(alpha: .12),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text('$rate%',
                          style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              color: PresenceRateBar.colorFor(rate))),
                    ),
                  ],
                ),
              );
            }),
          ],
        ),
      ),
    );
  }
}
