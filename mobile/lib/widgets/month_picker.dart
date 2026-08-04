import 'package:flutter/material.dart';

const kMonthNames = [
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'
];

/// شريط تنقّل بين الشهور (يُستخدم في تبويبات الموظف والأدمن).
class MonthPicker extends StatelessWidget {
  final int year;
  final int month;
  final void Function(int delta) onShift;

  const MonthPicker({
    super.key,
    required this.year,
    required this.month,
    required this.onShift,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      child: Row(
        children: [
          IconButton(
            onPressed: () => onShift(-1),
            icon: const Icon(Icons.chevron_right),
          ),
          Expanded(
            child: Text(
              '${kMonthNames[month - 1]} $year',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ),
          IconButton(
            onPressed: () => onShift(1),
            icon: const Icon(Icons.chevron_left),
          ),
        ],
      ),
    );
  }
}

/// بلاطة مؤشر ملوّنة (تُستخدم في شبكات الإحصائيات).
class StatTile extends StatelessWidget {
  final String label;
  final String value;
  final Color color;
  final IconData icon;

  const StatTile(this.label, this.value, this.color, this.icon, {super.key});

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
                    style: const TextStyle(fontSize: 13, color: Colors.grey)),
              ),
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

/// شريط نسبة الحضور مع تلوين حسب القيمة.
class PresenceRateBar extends StatelessWidget {
  final double rate;
  final String label;

  const PresenceRateBar({super.key, required this.rate, this.label = 'نسبة الحضور'});

  static Color colorFor(double rate) => rate >= 90
      ? Colors.green
      : rate >= 75
          ? Colors.orange
          : Colors.red;

  @override
  Widget build(BuildContext context) {
    final color = colorFor(rate);
    return Container(
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
              Text(label,
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
              const Spacer(),
              Text('$rate%',
                  style: TextStyle(
                      fontSize: 20, fontWeight: FontWeight.bold, color: color)),
            ],
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: (rate / 100).clamp(0.0, 1.0),
              minHeight: 10,
              backgroundColor: Colors.blueGrey.shade50,
              valueColor: AlwaysStoppedAnimation(color),
            ),
          ),
        ],
      ),
    );
  }
}
