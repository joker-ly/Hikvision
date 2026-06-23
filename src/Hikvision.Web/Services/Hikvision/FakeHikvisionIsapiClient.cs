using Hikvision.Web.Data;
using Hikvision.Web.Services.Hikvision.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Hikvision;

/// <summary>
/// عميل وهمي للتطوير والاختبار دون جهاز فعلي.
/// يولّد بصمات دخول/خروج تجريبية لكل موظف موجود في قاعدة البيانات ضمن المدى المطلوب.
/// يُفعَّل عبر الإعداد Device:UseFakeClient = true.
/// </summary>
public class FakeHikvisionIsapiClient : IHikvisionIsapiClient
{
    private readonly AppDbContext _db;

    public FakeHikvisionIsapiClient(AppDbContext db) => _db = db;

    public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
        => Task.FromResult((true, "عميل تجريبي (Fake) — لا يوجد جهاز حقيقي. الاتصال محاكى بنجاح."));

    public async IAsyncEnumerable<AcsEventInfo> GetEventsAsync(
        DateTimeOffset start, DateTimeOffset end,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.DeviceEmployeeNo, e.FullName })
            .ToListAsync(ct);

        // لكل يوم ضمن المدى، نولّد دخولًا قرابة الثامنة وخروجًا قرابة الرابعة لكل موظف.
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
                continue; // عطلة نهاية الأسبوع

            foreach (var emp in employees)
            {
                var checkIn = new DateTimeOffset(day, start.Offset).AddHours(8).AddMinutes(Random.Shared.Next(-15, 20));
                var checkOut = new DateTimeOffset(day, start.Offset).AddHours(16).AddMinutes(Random.Shared.Next(0, 30));

                if (checkIn >= start && checkIn <= end)
                    yield return Make(emp.DeviceEmployeeNo, emp.FullName, checkIn, "checkIn");
                if (checkOut >= start && checkOut <= end)
                    yield return Make(emp.DeviceEmployeeNo, emp.FullName, checkOut, "checkOut");
            }
        }
    }

    public async IAsyncEnumerable<DeviceUser> GetUsersAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        // أشخاص تجريبيون للاختبار دون جهاز
        yield return new DeviceUser("1", "أحمد محمد");
        yield return new DeviceUser("2", "سارة علي");
        yield return new DeviceUser("3", "خالد حسن");
    }

    private static AcsEventInfo Make(string empNo, string name, DateTimeOffset time, string status) => new()
    {
        EmployeeNoString = empNo,
        Name = name,
        Time = time.ToString("yyyy-MM-ddTHH:mm:sszzz"),
        AttendanceStatus = status,
        CurrentVerifyMode = "face",
        SerialNo = time.ToUnixTimeSeconds()
    };
}
