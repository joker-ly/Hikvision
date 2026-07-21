import 'package:flutter/material.dart';

import '../api.dart';
import 'account_tab.dart';
import 'month_tab.dart';
import 'records_tab.dart';
import 'today_tab.dart';

/// الهيكل الرئيسي: أربعة تبويبات (اليوم/شهري/سجلاتي/حسابي).
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  static const _tabs = [
    TodayTab(),
    MonthTab(),
    RecordsTab(),
    AccountTab(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(Api.fullName.isEmpty ? 'حضوري' : Api.fullName),
        backgroundColor: const Color(0xFF1E3A8A),
        foregroundColor: Colors.white,
      ),
      body: IndexedStack(index: _index, children: _tabs),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.today), label: 'اليوم'),
          NavigationDestination(icon: Icon(Icons.calendar_month), label: 'شهري'),
          NavigationDestination(icon: Icon(Icons.list_alt), label: 'سجلاتي'),
          NavigationDestination(icon: Icon(Icons.person), label: 'حسابي'),
        ],
      ),
    );
  }
}
