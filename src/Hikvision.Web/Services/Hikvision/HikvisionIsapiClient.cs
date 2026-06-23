using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.Hikvision.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Hikvision;

/// <summary>
/// عميل ISAPI حقيقي. يقرأ إعدادات الجهاز من قاعدة البيانات وقت التشغيل،
/// ويستخدم مصادقة Digest عبر HttpClientHandler (يتعامل مع تحدّي 401 تلقائيًا).
/// </summary>
public class HikvisionIsapiClient : IHikvisionIsapiClient
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<HikvisionIsapiClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HikvisionIsapiClient(AppDbContext db, IConfiguration config, ILogger<HikvisionIsapiClient> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private async Task<DeviceConfig> GetConfigAsync(CancellationToken ct)
    {
        var cfg = await _db.DeviceConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null)
            throw new InvalidOperationException("لم يتم ضبط إعدادات الجهاز بعد. الرجاء تعبئتها من شاشة الإعدادات.");
        return cfg;
    }

    private HttpClient CreateClient(DeviceConfig cfg)
    {
        var inner = new HttpClientHandler
        {
            // أجهزة LAN تستخدم غالبًا شهادات ذاتية التوقيع
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        // مصادقة Digest يدوية لتعمل مع طلبات POST (سحب الأحداث)
        var digest = new DigestAuthHandler(cfg.Username, cfg.Password) { InnerHandler = inner };

        var client = new HttpClient(digest, disposeHandler: true)
        {
            BaseAddress = new Uri(cfg.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_config.GetValue("Device:TimeoutSeconds", 30))
        };
        return client;
    }

    private static string FormatIsapiTime(DateTimeOffset dt) =>
        dt.ToString("yyyy-MM-ddTHH:mm:sszzz");

    public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var cfg = await GetConfigAsync(ct);
            using var client = CreateClient(cfg);
            var resp = await client.GetAsync("/ISAPI/System/deviceInfo?format=json", ct);
            if (resp.IsSuccessStatusCode)
                return (true, "تم الاتصال بالجهاز بنجاح.");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "فشلت المصادقة: تحقق من اسم المستخدم وكلمة المرور.");
            return (false, $"استجابة غير متوقعة من الجهاز: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "فشل اختبار الاتصال بالجهاز");
            return (false, $"تعذّر الوصول إلى الجهاز: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<AcsEventInfo> GetEventsAsync(
        DateTimeOffset start, DateTimeOffset end, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var cfg = await GetConfigAsync(ct);
        using var client = CreateClient(cfg);

        var maxResults = _config.GetValue("Device:MaxResultsPerPage", 30);
        var searchId = Guid.NewGuid().ToString();
        var position = 0;

        while (!ct.IsCancellationRequested)
        {
            var req = new AcsEventCondRequest
            {
                AcsEventCond = new AcsEventCond
                {
                    SearchID = searchId,
                    SearchResultPosition = position,
                    MaxResults = maxResults,
                    Major = 5,
                    Minor = 0,
                    StartTime = FormatIsapiTime(start),
                    EndTime = FormatIsapiTime(end)
                }
            };

            using var httpResp = await client.PostAsJsonAsync(
                "/ISAPI/AccessControl/AcsEvent?format=json", req, JsonOpts, ct);
            httpResp.EnsureSuccessStatusCode();

            var payload = await httpResp.Content.ReadFromJsonAsync<AcsEventResponse>(JsonOpts, ct);
            var result = payload?.AcsEvent;
            if (result is null || result.InfoList.Count == 0)
                yield break;

            foreach (var info in result.InfoList)
            {
                // بعض السجلات لا ترتبط بموظف (مثل أحداث الأبواب) — نتخطاها لاحقًا في طبقة المزامنة
                yield return info;
            }

            position += result.NumOfMatches;

            // التوقف عند انتهاء النتائج
            if (!string.Equals(result.ResponseStatusStrg, "MORE", StringComparison.OrdinalIgnoreCase))
                yield break;

            if (result.NumOfMatches <= 0)
                yield break;
        }
    }
}
