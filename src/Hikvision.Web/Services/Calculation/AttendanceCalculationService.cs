using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Calculation;

public class AttendanceCalculationService : IAttendanceCalculationService
{
    private readonly AppDbContext _db;

    public AttendanceCalculationService(AppDbContext db) => _db = db;

    public async Task<List<DailyAttendanceResult>> CalculateAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var employee = await _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("الموظف غير موجود.");

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var records = await _db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId && r.EventTime >= fromDt && r.EventTime <= toDt)
            .AsNoTracking()
            .ToListAsync(ct);

        return Calculate(employee.Group!, employee.Group!.Schedule, records, from, to);
    }

    public List<DailyAttendanceResult> Calculate(
        EmployeeGroup group, WorkSchedule? schedule,
        IReadOnlyCollection<AttendanceRecord> records, DateOnly from, DateOnly to)
    {
        var results = new List<DailyAttendanceResult>();
        var byDay = records.GroupBy(r => DateOnly.FromDateTime(r.EventTime))
                           .ToDictionary(g => g.Key, g => g.ToList());

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var dayRecords = byDay.TryGetValue(day, out var list) ? list : new List<AttendanceRecord>();
            var res = new DailyAttendanceResult
            {
                Date = day,
                IsWorkingDay = schedule?.IsWorkingDay(day.DayOfWeek) ?? true
            };

            // 1) السجلات اليدوية لها الأولوية في تحديد حالة اليوم
            var manual = dayRecords
                .Where(r => r.Source == AttendanceSource.Manual && r.ManualType != ManualAttendanceType.None)
                .OrderBy(r => r.EventTime)
                .FirstOrDefault();

            if (manual is not null)
            {
                res.ManualTypeApplied = manual.ManualType;
                res.Note = manual.Note;
                ApplyManualType(res, manual.ManualType);
                // ما زلنا نحسب أوقات/ساعات البصمات إن وُجدت
            }

            var deviceRecords = dayRecords
                .Where(r => r.Source == AttendanceSource.Device)
                .OrderBy(r => r.EventTime)
                .ToList();

            // 2) منطق الاحتساب حسب نوع المجموعة وطريقتها
            if (!group.IsTimeBound)
            {
                // المدراء: بصمة دخول واحدة تكفي، لا تأخير ولا ساعات مطلوبة
                CalculateManagers(res, deviceRecords);
            }
            else if (group.CalculationMode == CalculationMode.ByWorkHours)
            {
                CalculateByHours(res, deviceRecords, schedule);
            }
            else
            {
                CalculateByCheckInOut(res, deviceRecords, schedule);
            }

            // 3) تحديد الغياب: يوم عمل، لا حضور، ولا عذر يدوي
            if (res.ManualTypeApplied is ManualAttendanceType.None)
            {
                res.IsAbsent = res.IsWorkingDay && !res.IsPresent;
            }

            results.Add(res);
        }

        return results;
    }

    private static void ApplyManualType(DailyAttendanceResult res, ManualAttendanceType type)
    {
        switch (type)
        {
            case ManualAttendanceType.WorkMission:
            case ManualAttendanceType.TaskDone:
            case ManualAttendanceType.PermittedExit:
                res.IsPresent = true; // محسوب حضورًا
                res.IsAbsent = false;
                break;
            case ManualAttendanceType.Leave:
                res.IsPresent = false; // غياب بعذر — لا يُحتسب غيابًا غير مبرّر
                res.IsAbsent = false;
                break;
        }
    }

    private static void CalculateManagers(DailyAttendanceResult res, List<AttendanceRecord> device)
    {
        var ins = device.Where(r => r.Direction is PunchDirection.CheckIn or PunchDirection.Undefined).ToList();
        var firstIn = device.OrderBy(r => r.EventTime).FirstOrDefault();
        var lastOut = device.Where(r => r.Direction == PunchDirection.CheckOut)
                            .OrderByDescending(r => r.EventTime).FirstOrDefault();

        if (device.Count > 0)
        {
            res.IsPresent = true;
            res.FirstIn = firstIn?.EventTime;
            res.LastOut = lastOut?.EventTime;
            if (res.FirstIn.HasValue && res.LastOut.HasValue && res.LastOut > res.FirstIn)
                res.WorkedHours = (decimal)(res.LastOut.Value - res.FirstIn.Value).TotalHours;
        }
    }

    private static void CalculateByCheckInOut(DailyAttendanceResult res, List<AttendanceRecord> device, WorkSchedule? schedule)
    {
        var firstIn = device.Where(r => r.Direction is PunchDirection.CheckIn or PunchDirection.Undefined)
                           .OrderBy(r => r.EventTime).FirstOrDefault()
                       ?? device.OrderBy(r => r.EventTime).FirstOrDefault();
        var lastOut = device.Where(r => r.Direction == PunchDirection.CheckOut)
                           .OrderByDescending(r => r.EventTime).FirstOrDefault()
                       ?? device.OrderByDescending(r => r.EventTime).FirstOrDefault();

        if (firstIn is null) return;

        res.IsPresent = true;
        res.FirstIn = firstIn.EventTime;
        res.LastOut = lastOut?.EventTime;
        if (res.LastOut.HasValue && res.LastOut > res.FirstIn)
            res.WorkedHours = (decimal)(res.LastOut.Value - res.FirstIn.Value).TotalHours;

        // التأخير: الدخول بعد وقت البدء + سماح التأخير. الدخول قبل الموعد عادي.
        if (schedule?.StartTime is { } start)
        {
            var allowed = start.ToTimeSpan().Add(TimeSpan.FromMinutes(schedule.LateGraceMinutes));
            var actual = res.FirstIn.Value.TimeOfDay;
            if (actual > allowed)
            {
                res.IsLate = true;
                res.LateMinutes = (int)Math.Round((actual - allowed).TotalMinutes);
            }
        }
    }

    private static void CalculateByHours(DailyAttendanceResult res, List<AttendanceRecord> device, WorkSchedule? schedule)
    {
        // غير مقيّد بأوقات ثابتة: نجمع فترات العمل بمزاوجة الدخول/الخروج وطرح الاستراحات.
        var ordered = device.OrderBy(r => r.EventTime).ToList();
        if (ordered.Count == 0) return;

        res.FirstIn = ordered.First().EventTime;
        res.LastOut = ordered.Last().EventTime;

        decimal totalHours = 0m;
        DateTime? openIn = null;
        foreach (var r in ordered)
        {
            switch (r.Direction)
            {
                case PunchDirection.CheckIn:
                case PunchDirection.BreakIn:
                case PunchDirection.Undefined when openIn is null:
                    openIn ??= r.EventTime;
                    break;
                case PunchDirection.CheckOut:
                case PunchDirection.BreakOut:
                    if (openIn is { } start && r.EventTime > start)
                        totalHours += (decimal)(r.EventTime - start).TotalHours;
                    openIn = null;
                    break;
            }
        }
        // إذا لم تُزاوج البصمات، نعتمد الفارق بين أول وآخر بصمة
        if (totalHours == 0m && res.LastOut > res.FirstIn)
            totalHours = (decimal)(res.LastOut!.Value - res.FirstIn!.Value).TotalHours;

        res.WorkedHours = totalHours;

        var required = schedule?.RequiredDailyHours;
        res.IsPresent = required.HasValue ? totalHours >= required.Value : totalHours > 0m;
    }
}
