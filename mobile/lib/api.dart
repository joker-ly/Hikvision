import 'dart:convert';
import 'dart:io';

import 'package:device_info_plus/device_info_plus.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

/// استثناء يحمل رسالة خطأ عربية قادمة من الخادم.
class ApiException implements Exception {
  final String message;
  final int statusCode;
  ApiException(this.message, this.statusCode);
  @override
  String toString() => message;
}

/// عميل واجهة بوابة الموظفين — يدير عنوان الخادم والتوكن ومعرّف الجهاز.
class Api {
  static const _kServer = 'serverUrl';
  static const _kToken = 'token';
  static const _kDeviceId = 'deviceId';
  static const _kName = 'fullName';

  static late SharedPreferences _prefs;

  static Future<void> init() async {
    _prefs = await SharedPreferences.getInstance();
  }

  static String? get serverUrl => _prefs.getString(_kServer);
  static String? get token => _prefs.getString(_kToken);
  static String get fullName => _prefs.getString(_kName) ?? '';

  static Future<void> setServer(String url) async {
    var u = url.trim();
    if (u.endsWith('/')) u = u.substring(0, u.length - 1);
    await _prefs.setString(_kServer, u);
  }

  static Future<void> clearSession() async {
    await _prefs.remove(_kToken);
    await _prefs.remove(_kName);
  }

  /// معرّف الجهاز الثابت — أساس ربط الجهاز الواحد في الخادم.
  static Future<String> deviceId() async {
    var id = _prefs.getString(_kDeviceId);
    if (id == null) {
      id = const Uuid().v4();
      await _prefs.setString(_kDeviceId, id);
    }
    return id;
  }

  static Future<String> deviceDescription() async {
    try {
      final plugin = DeviceInfoPlugin();
      if (Platform.isAndroid) {
        final a = await plugin.androidInfo;
        return '${a.manufacturer} ${a.model} (Android ${a.version.release})';
      }
      if (Platform.isIOS) {
        final i = await plugin.iosInfo;
        return '${i.name} ${i.model} (iOS ${i.systemVersion})';
      }
    } catch (_) {}
    return 'جهاز غير معروف';
  }

  static Uri _uri(String path, [Map<String, String>? query]) =>
      Uri.parse('$serverUrl/api/portal$path').replace(queryParameters: query);

  static Map<String, dynamic> _decode(http.Response resp) {
    final body = resp.body.isEmpty
        ? <String, dynamic>{}
        : jsonDecode(utf8.decode(resp.bodyBytes)) as Map<String, dynamic>;
    if (resp.statusCode >= 200 && resp.statusCode < 300) return body;
    throw ApiException(
        body['error']?.toString() ?? 'خطأ غير متوقع (${resp.statusCode})',
        resp.statusCode);
  }

  /// فحص الوصول للخادم (شاشة الإعداد).
  static Future<bool> ping(String url) async {
    var u = url.trim();
    if (u.endsWith('/')) u = u.substring(0, u.length - 1);
    final resp = await http
        .get(Uri.parse('$u/api/portal/ping'))
        .timeout(const Duration(seconds: 6));
    return resp.statusCode == 200;
  }

  static Future<void> login(String employeeNo, String pin) async {
    final resp = await http
        .post(_uri('/login'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              'employeeNo': employeeNo,
              'pin': pin,
              'deviceId': await deviceId(),
              'deviceInfo': await deviceDescription(),
            }))
        .timeout(const Duration(seconds: 10));
    final data = _decode(resp);
    await _prefs.setString(_kToken, data['token'] as String);
    await _prefs.setString(_kName, data['fullName'] as String? ?? '');
  }

  static Future<Map<String, dynamic>> _get(String path,
      [Map<String, String>? query]) async {
    final resp = await http.get(_uri(path, query), headers: {
      'Authorization': 'Bearer $token',
    }).timeout(const Duration(seconds: 10));
    return _decode(resp);
  }

  static Future<Map<String, dynamic>> today() => _get('/today');

  static Future<Map<String, dynamic>> summary(int year, int month) =>
      _get('/summary', {'year': '$year', 'month': '$month'});

  static Future<Map<String, dynamic>> records(int year, int month) =>
      _get('/records', {'year': '$year', 'month': '$month'});

  static Future<Map<String, dynamic>> me() => _get('/me');

  static Future<void> logout() async {
    try {
      await http.post(_uri('/logout'), headers: {
        'Authorization': 'Bearer $token',
      }).timeout(const Duration(seconds: 6));
    } catch (_) {}
    await clearSession();
  }
}
