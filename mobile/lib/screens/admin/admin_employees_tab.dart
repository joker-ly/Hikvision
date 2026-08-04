import 'dart:async';

import 'package:flutter/material.dart';

import '../../api.dart';
import '../../main.dart';
import '../../widgets/error_view.dart';
import 'admin_employee_detail.dart';

/// بحث الموظفين وفتح إحصائيات أي موظف.
class AdminEmployeesTab extends StatefulWidget {
  const AdminEmployeesTab({super.key});

  @override
  State<AdminEmployeesTab> createState() => _AdminEmployeesTabState();
}

class _AdminEmployeesTabState extends State<AdminEmployeesTab> {
  final _controller = TextEditingController();
  Timer? _debounce;
  late Future<Map<String, dynamic>> _future;

  @override
  void initState() {
    super.initState();
    _future = Api.searchEmployees('');
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String q) {
    // بحث فوري مع تأخير بسيط لتقليل الطلبات
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      if (mounted) {
        setState(() {
          _future = Api.searchEmployees(q);
        });
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
          child: TextField(
            controller: _controller,
            textInputAction: TextInputAction.search,
            onChanged: _onChanged,
            decoration: InputDecoration(
              hintText: 'ابحث بالاسم أو الرقم المالي أو رقم البصمة',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _controller.text.isEmpty
                  ? null
                  : IconButton(
                      icon: const Icon(Icons.clear),
                      onPressed: () {
                        _controller.clear();
                        _onChanged('');
                        setState(() {});
                      },
                    ),
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
                  onRetry: () async {
                    setState(() {
                      _future = Api.searchEmployees(_controller.text);
                    });
                  },
                );
              }
              final list =
                  (snap.data!['employees'] as List).cast<Map<String, dynamic>>();
              if (list.isEmpty) {
                return const Center(
                  child: Text('لا نتائج مطابقة.',
                      style: TextStyle(color: Colors.grey)),
                );
              }

              return ListView.builder(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                itemCount: list.length,
                itemBuilder: (context, i) {
                  final e = list[i];
                  return Card(
                    margin: const EdgeInsets.only(bottom: 8),
                    child: ListTile(
                      onTap: () => Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => AdminEmployeeDetail(
                          employeeId: e['id'] as int,
                          fullName: e['fullName'] as String,
                        ),
                      )),
                      leading: CircleAvatar(
                        backgroundColor:
                            const Color(0xFF1E3A8A).withValues(alpha: .1),
                        child: const Icon(Icons.person,
                            color: Color(0xFF1E3A8A), size: 20),
                      ),
                      title: Text(e['fullName'] as String,
                          style: const TextStyle(
                              fontSize: 15, fontWeight: FontWeight.w600)),
                      subtitle: Text(
                        [
                          if (e['groupName'] != null) e['groupName'] as String,
                          'بصمة ${e['employeeNo']}',
                          if (e['financialNo'] != null)
                            'مالي ${e['financialNo']}',
                        ].join(' · '),
                        style: const TextStyle(fontSize: 12),
                      ),
                      trailing:
                          const Icon(Icons.chevron_left, color: Colors.grey),
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
