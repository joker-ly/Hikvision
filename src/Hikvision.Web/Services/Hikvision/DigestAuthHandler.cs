using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Hikvision.Web.Services.Hikvision;

/// <summary>
/// معالج مصادقة Digest يدوي.
/// مصادقة Digest المدمجة في .NET تفشل مع طلبات POST التي تحمل جسمًا، لأن إعادة الإرسال
/// بعد تحدّي 401 لا تعيد إرسال الجسم. هذا المعالج يخزّن الجسم مؤقتًا، يلتقط تحدّي 401،
/// يحسب ترويسة Authorization، ثم يعيد الإرسال مرة واحدة بنجاح.
/// </summary>
public class DigestAuthHandler : DelegatingHandler
{
    private readonly string _username;
    private readonly string _password;

    public DigestAuthHandler(string username, string password)
    {
        _username = username;
        _password = password;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // تخزين الجسم مؤقتًا لإتاحة إعادة الإرسال
        byte[]? body = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(ct);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        var first = await base.SendAsync(CloneRequest(request, body, contentType), ct);
        if (first.StatusCode != HttpStatusCode.Unauthorized)
            return first;

        var challenge = first.Headers.WwwAuthenticate
            .FirstOrDefault(h => string.Equals(h.Scheme, "Digest", StringComparison.OrdinalIgnoreCase));
        if (challenge?.Parameter is null)
            return first;

        first.Dispose();

        var authHeader = BuildDigestHeader(
            request.Method.Method,
            request.RequestUri!.PathAndQuery,
            challenge.Parameter);

        var second = CloneRequest(request, body, contentType);
        second.Headers.Authorization = new AuthenticationHeaderValue("Digest", authHeader);
        return await base.SendAsync(second, ct);
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

    private string BuildDigestHeader(string method, string uri, string challenge)
    {
        var p = ParseChallenge(challenge);
        p.TryGetValue("realm", out var realm);
        p.TryGetValue("nonce", out var nonce);
        p.TryGetValue("qop", out var qop);
        p.TryGetValue("opaque", out var opaque);
        p.TryGetValue("algorithm", out var algorithm);

        realm ??= string.Empty;
        nonce ??= string.Empty;

        var cnonce = Guid.NewGuid().ToString("N")[..16];
        const string nc = "00000001";

        var ha1 = Md5($"{_username}:{realm}:{_password}");
        var ha2 = Md5($"{method}:{uri}");

        string response;
        var sb = new StringBuilder();
        sb.Append($"username=\"{_username}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\"");

        if (!string.IsNullOrEmpty(qop))
        {
            // قد يحتوي qop على قائمة مثل "auth,auth-int" — نختار auth
            var qopValue = qop.Split(',').Select(x => x.Trim())
                .FirstOrDefault(x => x.Equals("auth", StringComparison.OrdinalIgnoreCase)) ?? "auth";
            response = Md5($"{ha1}:{nonce}:{nc}:{cnonce}:{qopValue}:{ha2}");
            sb.Append($", qop={qopValue}, nc={nc}, cnonce=\"{cnonce}\"");
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

    private static Dictionary<string, string> ParseChallenge(string challenge)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // key=value أو key="value" — يلتقط القيم المقتبسة وغير المقتبسة
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
