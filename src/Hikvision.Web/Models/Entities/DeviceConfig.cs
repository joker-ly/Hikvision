using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>إعدادات الاتصال بجهاز Hikvision (صف واحد، قابل للتعديل من شاشة الإعدادات).</summary>
public class DeviceConfig
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان الجهاز مطلوب"), MaxLength(255)]
    [Display(Name = "عنوان الجهاز (IP)")]
    public string Host { get; set; } = "192.168.1.64";

    [Range(1, 65535)]
    [Display(Name = "المنفذ")]
    public int Port { get; set; } = 80;

    [Display(Name = "استخدام HTTPS")]
    public bool UseHttps { get; set; } = false;

    [Required(ErrorMessage = "اسم المستخدم مطلوب"), MaxLength(100)]
    [Display(Name = "اسم المستخدم")]
    public string Username { get; set; } = "admin";

    [Required(ErrorMessage = "كلمة المرور مطلوبة"), MaxLength(255)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    /// <summary>آخر وقت مزامنة ناجح — يُستخدم كنقطة بداية للمزامنة التالية.</summary>
    [Display(Name = "آخر مزامنة")]
    public DateTime? LastSyncTime { get; set; }

    /// <summary>تاريخ آخر تنفيذ للمزامنة المجدولة (لمنع التكرار والتقاط الموعد الفائت).</summary>
    public DateTime? LastScheduledSyncDate { get; set; }

    /// <summary>الرابط الأساسي للجهاز.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string BaseUrl => $"{(UseHttps ? "https" : "http")}://{Host}:{Port}";
}
