using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>سجل تدقيق لكل عملية مزامنة مع الجهاز.</summary>
public class SyncLog
{
    public int Id { get; set; }

    [Display(Name = "بدأت في")]
    public DateTime StartedAtUtc { get; set; }

    [Display(Name = "انتهت في")]
    public DateTime? FinishedAtUtc { get; set; }

    [Display(Name = "من")]
    public DateTime FromTime { get; set; }

    [Display(Name = "إلى")]
    public DateTime ToTime { get; set; }

    [Display(Name = "عدد المسحوب")]
    public int FetchedCount { get; set; }

    [Display(Name = "عدد المُدخل")]
    public int InsertedCount { get; set; }

    [Display(Name = "مكرر (تم تخطيه)")]
    public int SkippedDuplicateCount { get; set; }

    [Display(Name = "موظفون غير مطابَقين")]
    public int UnmatchedEmployeeCount { get; set; }

    [Display(Name = "نجحت")]
    public bool Success { get; set; }

    [MaxLength(2000)]
    [Display(Name = "رسالة الخطأ")]
    public string? ErrorMessage { get; set; }
}
