using Hikvision.Web.Models.Entities;

namespace Hikvision.Web.Services.Calculation;

public interface IAttendanceCalculationService
{
    /// <summary>احتساب حضور موظف واحد ضمن مدى التواريخ (يحمّل بياناته من قاعدة البيانات).</summary>
    Task<List<DailyAttendanceResult>> CalculateAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>تحميل تواريخ الإجازات الرسمية ضمن المدى كمجموعة تواريخ مفردة.</summary>
    Task<ISet<DateOnly>> LoadHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>احتساب الحضور من بيانات محمّلة مسبقًا (يُستخدم في التقارير المجمّعة).</summary>
    List<DailyAttendanceResult> Calculate(
        EmployeeGroup group, WorkSchedule? schedule,
        IReadOnlyCollection<AttendanceRecord> records, DateOnly from, DateOnly to,
        ISet<DateOnly> holidays, DateOnly? exemptionDate = null);
}
