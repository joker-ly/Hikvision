import 'package:flutter/material.dart';

import '../../api.dart';
import 'admin_account_tab.dart';
import 'admin_employees_tab.dart';
import 'admin_stats_tab.dart';

/// واجهة المدير: الإحصائيات · الموظفون · حسابي.
class AdminShell extends StatefulWidget {
  const AdminShell({super.key});

  @override
  State<AdminShell> createState() => _AdminShellState();
}

class _AdminShellState extends State<AdminShell> {
  int _index = 0;

  static const _tabs = [
    AdminStatsTab(),
    AdminEmployeesTab(),
    AdminAccountTab(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Column(
          children: [
            Text(Api.fullName.isEmpty ? 'لوحة المدير' : Api.fullName,
                style: const TextStyle(fontSize: 17)),
            const Text('لوحة التقارير والإحصائيات',
                style: TextStyle(fontSize: 11, color: Colors.white70)),
          ],
        ),
      ),
      body: IndexedStack(index: _index, children: _tabs),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.bar_chart), label: 'الإحصائيات'),
          NavigationDestination(icon: Icon(Icons.people), label: 'الموظفون'),
          NavigationDestination(icon: Icon(Icons.person), label: 'حسابي'),
        ],
      ),
    );
  }
}
