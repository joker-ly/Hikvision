import 'package:flutter/material.dart';

import '../api.dart';

/// عرض موحّد لأخطاء الاتصال: أيقونة + الرسالة + زر إعادة المحاولة.
/// مبني على ListView ليعمل داخل RefreshIndicator أيضًا.
class ConnectionErrorView extends StatelessWidget {
  final Object error;
  final Future<void> Function()? onRetry;

  const ConnectionErrorView({super.key, required this.error, this.onRetry});

  @override
  Widget build(BuildContext context) {
    final isOffline = error is ApiException && (error as ApiException).isOffline;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        const SizedBox(height: 40),
        Icon(
          isOffline ? Icons.wifi_off : Icons.error_outline,
          size: 60,
          color: isOffline ? Colors.orange.shade700 : Colors.grey,
        ),
        const SizedBox(height: 16),
        Text(
          error.toString(),
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
        if (isOffline) ...[
          const SizedBox(height: 8),
          Text(
            'تأكد من اتصالك بشبكة الوزارة ثم أعد المحاولة.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 13, color: Colors.grey.shade600),
          ),
        ],
        if (onRetry != null) ...[
          const SizedBox(height: 18),
          Center(
            child: FilledButton.icon(
              onPressed: () => onRetry!(),
              icon: const Icon(Icons.refresh),
              label: const Text('إعادة المحاولة'),
            ),
          ),
        ],
      ],
    );
  }
}
