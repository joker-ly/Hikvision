using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Enums;

/// <summary>
/// طريقة احتساب الحضور لمجموعة التوظيف.
/// مفتاح التبديل بين الاعتماد على أوقات الحضور/الانصراف أو على عدد ساعات العمل.
/// </summary>
public enum CalculationMode
{
    /// <summary>الاحتساب بأوقات الحضور والانصراف (التقيد بمواعيد الدوام).</summary>
    [Display(Name = "بأوقات الحضور والانصراف")]
    ByCheckInOut = 0,

    /// <summary>الاحتساب بعدد ساعات العمل (غير مقيّد بمواعيد ثابتة).</summary>
    [Display(Name = "بعدد ساعات العمل")]
    ByWorkHours = 1
}
