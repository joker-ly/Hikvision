using System.ComponentModel.DataAnnotations;
using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.Models.Entities;

/// <summary>
/// سجل حضور موحّد لكلٍ من بصمات الجهاز والإدخالات اليدوية.
/// يُميَّز السجل اليدوي عن سجل الجهاز عبر الحقل <see cref="Source"/>.
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>وقت الحدث (محوّل للمنطقة الزمنية المُعدّة).</summary>
    [Display(Name = "وقت الحدث")]
    public DateTime EventTime { get; set; }

    /// <summary>مصدر السجل: جهاز أو يدوي.</summary>
    [Display(Name = "المصدر")]
    public AttendanceSource Source { get; set; }

    /// <summary>اتجاه البصمة (للسجلات القادمة من الجهاز).</summary>
    [Display(Name = "الاتجاه")]
    public PunchDirection Direction { get; set; } = PunchDirection.Undefined;

    /// <summary>نوع السجل اليدوي (يُحدَّد فقط عند Source = Manual).</summary>
    [Display(Name = "نوع السجل اليدوي")]
    public ManualAttendanceType ManualType { get; set; } = ManualAttendanceType.None;

    /// <summary>ملاحظة/سبب — إلزامية للإدخالات اليدوية.</summary>
    [MaxLength(1000)]
    [Display(Name = "ملاحظة")]
    public string? Note { get; set; }

    /// <summary>معرّف المستخدم الذي أدخل السجل يدويًا (للتدقيق).</summary>
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // حقول مصدر الجهاز
    [MaxLength(100)]
    public string? SerialNo { get; set; }

    [MaxLength(100)]
    public string? VerifyMode { get; set; }

    [MaxLength(500)]
    public string? PictureUrl { get; set; }
}
