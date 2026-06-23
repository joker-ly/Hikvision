using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Hikvision.Web.Services.Hikvision;

/// <summary>
/// معالج مصادقة Digest يدوي مع تخزين الـ nonce.
/// مصادقة Digest المدمجة في .NET تفشل مع طلبات POST التي تحمل جسمًا. كما أن إعادة
/// المصادقة في كل طلب تضاعف عدد الطلبات. هذا المعالج يحسب الترويسة يدويًا، ويعيد
/// استخدام الـ nonce عبر الطلبات المتتالية (مع زيادة nc) لتجنّب تحدّي 401 المتكرر.
/// </summary>
public class DigestAuthHandler : DelegatingHandler
{
    private readonly string _username;
    private readonly string _password;
    private readonly object _lock = new();
    private Dictionary<string, string>? _challenge;
    private int _nc;

    public DigestAuthHandler(string username, string password)
    {
        _username = username;
        _password = password;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        byte[]? body = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(ct);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        var method = request.Method.Method;
        var uri = request.RequestUri!.PathAndQuery;

        // محاولة أولى باستخدام تحدٍّ مخزَّن إن وُجد (لتفادي 401)
        var precomputed = ComputeAuth(method, uri);
        var first = CloneRequest(request, body, contentType);
        if (precomputed is not null)
            first.Headers.Authorization = new AuthenticationHeaderValue("Digest", precomputed);

        var resp = await base.SendAsync(first, ct);
        if (resp.StatusCode != HttpStatusCode.Unauthorized)
            return resp;

        // التحدّي المخزَّن منتهٍ أو غير موجود — نحدّثه من الاستجابة ونعيد المحاولة
        var challenge = resp.Headers.WwwAuthenticate
            .FirstOrDefault(h => string.Equals(h.Scheme, "Digest", StringComparison.OrdinalIgnoreCase));
        if (challenge?.Parameter is null)
            return resp;

        resp.Dispose();

        lock (_lock)
        {
            _challenge = ParseChallenge(challenge.Parameter);
            _nc = 0;
        }

        var authHeader = ComputeAuth(method, uri);
        var second = CloneRequest(request, body, contentType);
        if (authHeader is not null)
            second.Headers.Authorization = new AuthenticationHeaderValue("Digest", authHeader);
        return await base.SendAsync(second, ct);
    }

    /// <summary>يحسب ترويسة Digest من التحدّي المخزَّن (مع زيادة nc)، أو null إن لم يوجد تحدٍّ.</summary>
    private string? ComputeAuth(string method, string uri)
    {
        Dictionary<string, string> p;
        int nc;
        lock (_lock)
        {
            if (_challenge is null) return null;
            p = _challenge;
            nc = ++_nc;
        }

        p.TryGetValue("realm", out var realm);
        p.TryGetValue("nonce", out var nonce);
        p.TryGetValue("qop", out var qop);
        p.TryGetValue("opaque", out var opaque);
        p.TryGetValue("algorithm", out var algorithm);

        realm ??= string.Empty;
        nonce ??= string.Empty;

        var ncValue = nc.ToString("x8");
        var cnonce = Guid.NewGuid().ToString("N")[..16];

        var ha1 = Md5($"{_username}:{realm}:{_password}");
        var ha2 = Md5($"{method}:{uri}");

        string response;
        var sb = new StringBuilder();
        sb.Append($"username=\"{_username}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\"");

        if (!string.IsNullOrEmpty(qop))
        {
            var qopValue = qop.Split(',').Select(x => x.Trim())
                .FirstOrDefault(x => x.Equals("auth", StringComparison.OrdinalIgnoreCase)) ?? "auth";
            response = Md5($"{ha1}:{nonce}:{ncValue}:{cnonce}:{qopValue}:{ha2}");
            sb.Append($", qop={qopValue}, nc={ncValue}, cnonce=\"{cnonce}\"");
        }
        else
        {
            response = Md5($"{ha1}:{nonce}:{ha2}");
        }

        sb.Append($", response=\"{response}\"");
        if (!string.IsNullOrEmpty(algorithm)) sb.Append($", algorithm={algorithm}");
        if (!string.IsNullOrEmpty(opaque)) sb.Append($", opaque=\"{opaque}\"");

        return sb.ToString();
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage src, byte[]? body, string? contentType)
    {
        var clone = new HttpRequestMessage(src.Method, src.RequestUri) { Version = src.Version };
        foreach (var h in src.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            if (contentType is not null)
                clone.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }
        return clone;
    }

    private static Dictionary<string, string> ParseChallenge(string challenge)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = System.Text.RegularExpressions.Regex.Matches(
            challenge, "(\\w+)\\s*=\\s*(?:\"([^\"]*)\"|([^,]*))");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var key = m.Groups[1].Value;
            var value = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value.Trim();
            if (!string.IsNullOrEmpty(key))
                dict[key] = value;
        }
        return dict;
    }

    private static string Md5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
