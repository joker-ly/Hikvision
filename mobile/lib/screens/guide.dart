import 'package:flutter/material.dart';

import '../api.dart';
import '../main.dart';
import 'lock_screen.dart';
import 'login.dart';

/// شاشة تعليمات تشرح آلية عمل التطبيق ونوافذه — تُعرض عند أول تشغيل،
/// ويمكن فتحها لاحقًا من تبويب "حسابي".
class GuideScreen extends StatefulWidget {
  /// true عند العرض من داخل التطبيق (زر إغلاق بدل "ابدأ").
  final bool asHelp;
  const GuideScreen({super.key, this.asHelp = false});

  @override
  State<GuideScreen> createState() => _GuideScreenState();
}

class _GuidePage {
  final IconData icon;
  final Color color;
  final String title;
  final List<String> lines;
  const _GuidePage(this.icon, this.color, this.title, this.lines);
}

class _GuideScreenState extends State<GuideScreen> {
  final _controller = PageController();
  int _index = 0;

  static const _brand = Color(0xFF1E3A8A);

  static const _pages = <_GuidePage>[
    _GuidePage(Icons.fingerprint, _brand, 'مرحبًا بك في «حضوري»', [
      'تطبيق رسمي لموظفي الوزارة لمتابعة الحضور والانصراف من هاتفك.',
      'يقرأ التطبيق بصماتك من جهاز البصمة ويعرضها لك بشكل منظّم.',
      'البيانات للعرض فقط — لا يمكن تعديلها من التطبيق.',
    ]),
    _GuidePage(Icons.wifi_tethering, Colors.teal, 'شبكة الوزارة والمزامنة', [
      'يعمل التطبيق داخل شبكة الوزارة فقط؛ خارجها تظهر رسالة تنبيه.',
      'تُزامَن البصمات مع الجهاز كل 15 دقيقة من 8:00 صباحًا حتى 3:00 عصرًا.',
      'بصمتك الجديدة لا تظهر فورًا، بل بعد اكتمال المزامنة التالية.',
      'يوجد مؤقّت في التطبيق يعرض وقت المزامنة القادمة.',
    ]),
    _GuidePage(Icons.dashboard_customize, Colors.indigo, 'نوافذ التطبيق', [
      '• اليوم: أول دخول وآخر خروج وحالة التأخير اليوم.',
      '• شهري: أيام الحضور والغياب، مرات ودقائق التأخير، ساعات العمل ونسبة الحضور.',
      '• سجلاتي: كل بصماتك يومًا بيوم لأي شهر تختاره.',
      '• حسابي: بياناتك ووردية دوامك وجهازك المرتبط والدخول بالبصمة.',
      'للمديرين: تظهر لوحة التقارير والإحصائيات وبحث الموظفين بدل الشاشات الشخصية.',
    ]),
    _GuidePage(Icons.security, Colors.deepPurple, 'الدخول والأمان', [
      'اسم المستخدم هو رقمك في جهاز البصمة، مع رقم سري خاص بك.',
      'يمكنك تفعيل «حفظ بيانات الدخول» و«الدخول بالبصمة» لتسهيل الفتح.',
      'حسابك مرتبط بجهاز واحد فقط لحماية بياناتك.',
      'عند تغيير هاتفك، راجع مكتب تقنية المعلومات لإعادة تعيين الجهاز.',
    ]),
    _GuidePage(Icons.support_agent, Colors.orange, 'الحصول على الرقم السري', [
      'للحصول على الرقم السري تواصل مع مدير مكتب تقنية المعلومات:',
      Api.supportPhone,
      'وكذلك لأي استفسار أو مشكلة في الدخول أو إعادة تعيين الجهاز.',
    ]),
  ];

  Future<void> _finish() async {
    if (widget.asHelp) {
      Navigator.of(context).pop();
      return;
    }
    await Api.setGuideSeen();
    if (!mounted) return;
    // الانتقال حسب حالة الجلسة
    if (Api.token == null) {
      goTo(context, const LoginScreen());
    } else if (Api.biometricEnabled) {
      goTo(context, const LockScreen());
    } else {
      goTo(context, homeForRole());
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLast = _index == _pages.length - 1;
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            // زر التخطي
            Align(
              alignment: AlignmentDirectional.topStart,
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: TextButton(
                  onPressed: _finish,
                  child: Text(widget.asHelp ? 'إغلاق' : 'تخطي'),
                ),
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _pages.length,
                onPageChanged: (i) => setState(() => _index = i),
                itemBuilder: (context, i) {
                  final p = _pages[i];
                  final isPhonePage = i == _pages.length - 1;
                  return Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 28),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Container(
                          width: 120,
                          height: 120,
                          decoration: BoxDecoration(
                            color: p.color.withValues(alpha: .1),
                            shape: BoxShape.circle,
                          ),
                          child: Icon(p.icon, size: 58, color: p.color),
                        ),
                        const SizedBox(height: 26),
                        Text(
                          p.title,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.bold,
                            color: p.color,
                          ),
                        ),
                        const SizedBox(height: 20),
                        ...p.lines.map((line) {
                          // إبراز رقم الهاتف في الصفحة الأخيرة
                          final isPhone = isPhonePage && line == Api.supportPhone;
                          return Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: isPhone
                                ? Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 24, vertical: 12),
                                    decoration: BoxDecoration(
                                      color: p.color.withValues(alpha: .12),
                                      borderRadius: BorderRadius.circular(14),
                                    ),
                                    child: Text(
                                      line,
                                      textDirection: TextDirection.ltr,
                                      style: TextStyle(
                                        fontSize: 26,
                                        fontWeight: FontWeight.bold,
                                        color: p.color,
                                        letterSpacing: 1.5,
                                      ),
                                    ),
                                  )
                                : Text(
                                    line,
                                    textAlign: TextAlign.center,
                                    style: const TextStyle(
                                      fontSize: 15.5,
                                      height: 1.7,
                                      color: Color(0xFF374151),
                                    ),
                                  ),
                          );
                        }),
                      ],
                    ),
                  );
                },
              ),
            ),
            // مؤشر الصفحات
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(_pages.length, (i) {
                final active = i == _index;
                return Container(
                  margin: const EdgeInsets.symmetric(horizontal: 4),
                  width: active ? 22 : 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: active ? _brand : Colors.blueGrey.shade200,
                    borderRadius: BorderRadius.circular(4),
                  ),
                );
              }),
            ),
            Padding(
              padding: const EdgeInsets.all(24),
              child: FilledButton.icon(
                onPressed: () {
                  if (isLast) {
                    _finish();
                  } else {
                    _controller.nextPage(
                      duration: const Duration(milliseconds: 300),
                      curve: Curves.easeOut,
                    );
                  }
                },
                icon: Icon(isLast
                    ? (widget.asHelp ? Icons.check : Icons.login)
                    : Icons.arrow_back),
                label: Text(isLast
                    ? (widget.asHelp ? 'تم' : 'ابدأ الاستخدام')
                    : 'التالي'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
