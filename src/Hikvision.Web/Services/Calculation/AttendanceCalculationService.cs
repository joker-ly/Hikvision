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

        var holidays = await LoadHolidaysAsync(from, to, ct);

        return Calculate(employee.Group!, employee.Group!.Schedule, records, from, to, holidays, employee.ExemptionDate);
    }

    /// <summary>تحميل كل تواريخ الإجازات الرسمية ضمن المدى كمجموعة تواريخ مفردة.</summary>
    public async Task<ISet<DateOnly>> LoadHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var holidays = await _db.Holidays
            .Where(h => h.StartDate <= to && h.EndDate >= from)
            .AsNoTracking().ToListAsync(ct);

        var set = new HashSet<DateOnly>();
        foreach (var h in holidays)
            for (var d = h.StartDate; d <= h.EndDate; d = d.AddDays(1))
                if (d >= from && d <= to) set.Add(d);
        return set;
    }

    public List<DailyAttendanceResult> Calculate(
        EmployeeGroup group, WorkSchedule? schedule,
        IReadOnlyCollection<AttendanceRecord> records, DateOnly from, DateOnly to,
        ISet<DateOnly> holidays, DateOnly? exemptionDate = null)
    {
        var results = new List<DailyAttendanceResult>();
        var byDay = records.GroupBy(r => DateOnly.FromDateTime(r.EventTime))
                           .ToDictionary(g => g.Key, g => g.ToList());

        // العطل الرسمية وعطل نهاية الأسبوع لا يتقرر احتسابها حضورًا إلا بعد معرفة
        // أيام الحضور الصحيح (ضمن المواعيد) — لذلك تُؤجَّل لنهاية الحساب.
        var deferredHolidays = new List<DailyAttendanceResult>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            // الإعفاء: لا يُحتسب أي يوم بعد تاريخ الإعفاء (لا حضور ولا غياب)
            if (exemptionDate is { } ex && day > ex)
                continue;

            var dayRecords = byDay.TryGetValue(day, out var list) ? list : new List<AttendanceRecord>();
            var res = new DailyAttendanceResult
            {
                Date = day,
                // عند غياب الوردية نعتبر الجمعة والسبت عطلة افتراضيًا (لا تُحتسب غيابًا)
                IsWorkingDay = schedule?.IsWorkingDay(day.DayOfWeek)
                    ?? (day.DayOfWeek != DayOfWeek.Friday && day.DayOfWeek != DayOfWeek.Saturday)
            };

            // 0) مجموعة معفاة من البصمة: كل يوم يُحتسب حضورًا كاملًا،
            //    إلا أيام الإجازة بدون مرتب فتُخصم حتى للمعفيين.
            if (group.IsFingerprintExempt)
            {
                var hasUnpaid = dayRecords.Any(r =>
                    r.Source == AttendanceSource.Manual && r.ManualType == ManualAttendanceType.UnpaidLeave);
                if (hasUnpaid)
                    res.IsUnpaidLeave = true;
                else
                    res.IsPresent = true;
                results.Add(res);
                continue;
            }

            // 1) الإجازات الرسمية المعمّمة: يُؤجَّل قرار احتسابها لنهاية الحساب
            if (holidays.Contains(day))
            {
                res.IsHoliday = true;
                deferredHolidays.Add(res);
                results.Add(res);
                continue;
            }

            // 2) عطلة نهاية الأسبوع (يوم غير عمل): يُؤجَّل قرار احتسابها لنهاية الحساب
            if (!res.IsWorkingDay)
            {
                deferredHolidays.Add(res);
                results.Add(res);
                continue;
            }

            // 2) السجلات اليدوية
            var manual = dayRecords
                .Where(r => r.Source == AttendanceSource.Manual && r.ManualType != ManualAttendanceType.None)
                .ToList();
            var primaryManual = manual.FirstOrDefault(r => r.ManualType != ManualAttendanceType.PermittedExit)
                                ?? manual.FirstOrDefault();
            res.HasPermittedExit = manual.Any(r => r.ManualType == ManualAttendanceType.PermittedExit);

            if (primaryManual is not null && primaryManual.ManualType != ManualAttendanceType.PermittedExit)
            {
                res.ManualTypeApplied = primaryManual.ManualType;
                res.Note = primaryManual.Note;
            }
            else if (res.HasPermittedExit)
            {
                res.ManualTypeApplied = ManualAttendanceType.PermittedExit;
                res.Note = manual.First(r => r.ManualType == ManualAttendanceType.PermittedExit).Note;
            }

            var deviceRecords = dayRecords
                .Where(r => r.Source == AttendanceSource.Device)
                .OrderBy(r => r.EventTime)
                .ToList();
            // السجلات اليدوية من نوع دخول/خروج (للإجازات/المهام تُولَّد بصمات يدوية)
            var manualPunches = manual
                .OrderBy(r => r.EventTime).ToList();
            var allPunches = deviceRecords.Concat(manualPunches).OrderBy(r => r.EventTime).ToList();

            // 3أ) إجازة بدون مرتب: لا تُحتسب حضورًا وتُخصم من الصرف
            if (res.ManualTypeApplied == ManualAttendanceType.UnpaidLeave)
            {
                res.IsUnpaidLeave = true;
                results.Add(res);
                continue;
            }

            // 3) أنواع يدوية تُحتسب حضورًا/عذرًا مباشرة
            if (res.ManualTypeApplied is ManualAttendanceType.Leave
                or ManualAttendanceType.WorkMission or ManualAttendanceType.TaskDone)
            {
                res.IsPresent = true;
                ComputeHours(res, allPunches);
                results.Add(res);
                continue;
            }

            // 4) منطق الاحتساب حسب نوع المجموعة
            if (!group.IsTimeBound)
            {
                CalculateManagers(res, allPunches);
            }
            else if (group.CalculationMode == CalculationMode.ByWorkHours)
            {
                CalculateByHours(res, allPunches, schedule);
            }
            else
            {
                CalculateByCheckInOut(res, allPunches, schedule, res.HasPermittedExit);
            }

            // 5) تحديد الغياب
            res.IsAbsent = res.IsWorkingDay && !res.IsPresent;

            results.Add(res);
        }

        // قرار احتساب العطل (رسمية + نهاية أسبوع):
        // - تُحتسب حضورًا فقط لمن له يوم حضور صحيح ضمن المواعيد (يوم عمل احتُسب حضورًا).
        //   من بصم خارج المواعيد واحتُسب غائبًا لا يُعتدّ ببصمته.
        // - وإن تأخر أول حضور صحيح أسبوعًا فأكثر عن بداية الفترة، تُحتسب العطل
        //   من أول يوم حضور صحيح فقط (ما قبله لا يُحتسب حتى لو وُجدت بصمات).
        var attendedDates = results
            .Where(r => r.IsWorkingDay && !r.IsHoliday && r.IsPresent)
            .Select(r => r.Date)
            .ToList();

        // العطلة غير المحتسبة حضورًا تُحتسب غيابًا — حتى يكون مجموع (حضور + غياب)
        // مغطّيًا للفترة كاملة، لأن المنظومة المالية تعتمد أيام الغياب مباشرة.
        if (attendedDates.Count == 0)
        {
            foreach (var d in deferredHolidays)
            {
                d.IsPresent = false;
                d.IsAbsent = true;
            }
        }
        else
        {
            var firstValid = attendedDates.Min();
            var longAbsence = firstValid.DayNumber - from.DayNumber >= 7;
            foreach (var d in deferredHolidays)
            {
                d.IsPresent = !longAbsence || d.Date >= firstValid;
                d.IsAbsent = !d.IsPresent;
            }
        }

        return results;
    }

    private static void ComputeHours(DailyAttendanceResult res, List<AttendanceRecord> punches)
    {
        if (punches.Count == 0) return;
        res.FirstIn = punches.First().EventTime;
        res.LastOut = punches.Last().EventTime;
        if (res.LastOut > res.FirstIn)
            res.WorkedHours = (decimal)(res.LastOut.Value - res.FirstIn.Value).TotalHours;
    }

    private static void CalculateManagers(DailyAttendanceResult res, List<AttendanceRecord> punches)
    {
        if (punches.Count == 0) return;
        res.IsPresent = true; // بصمة دخول واحدة تكفي
        ComputeHours(res, punches);
    }

    private static void CalculateByCheckInOut(
        DailyAttendanceResult res, List<AttendanceRecord> punches, WorkSchedule? schedule, bool hasPermittedExit)
    {
        if (punches.Count == 0) return; // لا حضور => غياب

        var firstIn = punches.First();
        res.FirstIn = firstIn.EventTime;
        res.LastOut = punches.Last().EventTime;
        if (res.LastOut > res.FirstIn)
            res.WorkedHours = (decimal)(res.LastOut.Value - res.FirstIn.Value).TotalHours;

        // التأخير: الحضور بعد موعد الدخول + السماحية => يُحتسب غيابًا
        if (schedule?.StartTime is { } start)
        {
            var allowed = start.ToTimeSpan().Add(TimeSpan.FromMinutes(schedule.LateGraceMinutes));
            if (res.FirstIn.Value.TimeOfDay > allowed)
            {
                res.IsLate = true;
                res.LateMinutes = (int)Math.Round((res.FirstIn.Value.TimeOfDay - allowed).TotalMinutes);
                res.IsPresent = false; // متأخر = غياب حسب السياسة
                return;
            }
        }

        // الخروج: أي بصمة من (موعد الخروج − سماح الخروج) فما بعده تُعتبر خروجًا نظاميًا
        if (schedule?.EndTime is { } end)
        {
            var checkoutThreshold = end.ToTimeSpan().Subtract(TimeSpan.FromMinutes(schedule.CheckoutGraceMinutes));
            var hasProperCheckout = punches.Any(p => p.EventTime.TimeOfDay >= checkoutThreshold);
            if (!hasProperCheckout)
            {
                res.IsEarlyLeave = true;
                // المغادرة قبل الموعد = غياب، ما لم يوجد إذن خروج
                res.IsPresent = hasPermittedExit;
                return;
            }
        }

        res.IsPresent = true;
    }

    private static void CalculateByHours(DailyAttendanceResult res, List<AttendanceRecord> punches, WorkSchedule? schedule)
    {
        if (punches.Count == 0) return;

        res.FirstIn = punches.First().EventTime;
        res.LastOut = punches.Last().EventTime;

        decimal totalHours = 0m;
        DateTime? openIn = null;
        foreach (var r in punches)
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
        if (totalHours == 0m && res.LastOut > res.FirstIn)
            totalHours = (decimal)(res.LastOut!.Value - res.FirstIn!.Value).TotalHours;

        res.WorkedHours = totalHours;
        var required = schedule?.RequiredDailyHours;
        res.IsPresent = required.HasValue ? totalHours >= required.Value : totalHours > 0m;
    }
}
