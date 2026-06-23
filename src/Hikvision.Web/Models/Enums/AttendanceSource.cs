namespace Hikvision.Web.Models.Enums;

/// <summary>
/// مصدر سجل الحضور: من الجهاز أو إدخال يدوي.
/// هذا الحقل هو ما يميّز السجل اليدوي عن سجل الجهاز.
/// </summary>
public enum AttendanceSource
{
    /// <summary>سُحب من جهاز Hikvision عبر ISAPI.</summary>
    Device = 0,

    /// <summary>أُدخل يدويًا بواسطة المدير.</summary>
    Manual = 1
}
