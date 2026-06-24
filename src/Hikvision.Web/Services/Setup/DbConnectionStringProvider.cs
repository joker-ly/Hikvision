using System.Text.Json;

namespace Hikvision.Web.Services.Setup;

/// <summary>
/// يدير سلسلة اتصال قاعدة البيانات أثناء التشغيل ويحفظها في ملف db-settings.json بجوار
/// التطبيق، حتى يمكن إعدادها من معالج الإعداد عند أول تشغيل دون تعديل appsettings يدويًا.
/// </summary>
public class DbConnectionStringProvider
{
    private readonly string _filePath;
    private readonly string? _fallback;
    private string? _cached;
    private volatile bool _isReady;

    public DbConnectionStringProvider(IHostEnvironment env, IConfiguration config)
    {
        _filePath = Path.Combine(env.ContentRootPath, "db-settings.json");
        _fallback = config.GetConnectionString("DefaultConnection");
        _cached = LoadFromFile();
    }

    /// <summary>سلسلة الاتصال الحالية: من الملف المحفوظ إن وُجد، وإلا من appsettings.</summary>
    public string? Current => _cached ?? _fallback;

    /// <summary>هل اكتملت تهيئة القاعدة بنجاح (ترحيل/زرع)؟ عند false يُعرض معالج الإعداد.</summary>
    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;

    /// <summary>حفظ سلسلة اتصال جديدة في الملف وتحديث القيمة الحالية.</summary>
    public void Save(string connectionString)
    {
        var json = JsonSerializer.Serialize(
            new DbSettingsFile { ConnectionString = connectionString },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
        _cached = connectionString;
    }

    private string? LoadFromFile()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var data = JsonSerializer.Deserialize<DbSettingsFile>(File.ReadAllText(_filePath));
            return string.IsNullOrWhiteSpace(data?.ConnectionString) ? null : data!.ConnectionString;
        }
        catch
        {
            return null;
        }
    }

    private sealed class DbSettingsFile
    {
        public string? ConnectionString { get; set; }
    }
}
