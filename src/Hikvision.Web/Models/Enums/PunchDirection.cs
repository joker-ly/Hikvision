namespace Hikvision.Web.Models.Enums;

/// <summary>
/// اتجاه البصمة، يُشتق من حقل attendanceStatus القادم من الجهاز.
/// </summary>
public enum PunchDirection
{
    /// <summary>غير محدد (الجهاز لم يحدد دخول/خروج).</summary>
    Undefined = 0,

    /// <summary>دخول (checkIn).</summary>
    CheckIn = 1,

    /// <summary>خروج (checkOut).</summary>
    CheckOut = 2,

    /// <summary>بداية استراحة (breakOut).</summary>
    BreakOut = 3,

    /// <summary>نهاية استراحة (breakIn).</summary>
    BreakIn = 4
}
