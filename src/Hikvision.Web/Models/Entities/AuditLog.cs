using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>سجل تدقيق لكل عملية تحدث في النظام (من، ماذا، متى).</summary>
public class AuditLog
{
    public int Id { get; set; }

    [MaxLength(150)]
    [Display(Name = "المستخدم")]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "العملية")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "التفاصيل")]
    public string? Details { get; set; }

    /// <summary>وقت العملية بالمنطقة الزمنية المحلية المُعدّة.</summary>
    [Display(Name = "التاريخ والوقت")]
    public DateTime TimestampLocal { get; set; }
}
