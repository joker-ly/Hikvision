using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Calculation;
using Hikvision.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Reports;

/// <summary>
/// يبني الإحصائيات من نفس خدمة الاحتساب المعتمدة في تقرير المرتبات،
/// فتأتي الأرقام متسقة مع قواعد الإعفاء والعطل والغياب المعمول بها.
/// </summary>
public class StatisticsReportService : IStatisticsReportService
{
    private static readonly CultureInfo ArCulture = CultureInfo.GetCultureInfo("ar");

    private readonly AppDbContext _db;
    private readonly IAttendanceCalculationService _calc;

    public StatisticsReportService(AppDbContext db, IAttendanceCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    public async Task<StatisticsViewModel> BuildAsync(int? groupId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var stats = new StatisticsViewModel { GroupId = groupId, From = from, To = to };

        var query = _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .Where(e => e.IsActive);
        if (groupId.HasValue)
            query = query.Where(e => e.EmployeeGroupId == groupId.Value);

        var employees = await query.AsNoTracking().ToListAsync(ct);

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);
        var holidays = await _calc.LoadHolidaysAsync(from, to, ct);

        // تجميعات يومية: تاريخ → عدّادات
        var daily = new Dictionary<DateOnly, DailyStatsRow>();
        for (var d = from; d <= to; d = d.AddDays(1))
            daily[d] = new DailyStatsRow
            {
                Date = d,
                DayName = ArCulture.DateTimeFormat.GetDayName(d.DayOfWeek),
                IsHoliday = holidays.Contains(d)
            };

        foreach (var emp in employees)
        {
            var records = await _db.AttendanceRecords
                .Where(r => r.EmployeeId == emp.Id && r.EventTime >= fromDt && r.EventTime <= toDt)
                .AsNoTracking().ToListAsync(ct);

            var days = _calc.Calculate(emp.Group!, emp.Group!.Schedule, records, from, to, holidays, emp.ExemptionDate);
            if (days.Count == 0) continue; // معفى طوال الفترة

            var present = days.Count(d => d.IsPresent);
            var absent = days.Count - present;

            var row = new EmployeeStatsRow
            {
                EmployeeId = emp.Id,
                FullName = emp.FullName,
                FinancialNo = emp.FinancialNo,
                GroupName = emp.Group!.Name,
                PeriodDays = days.Count,
                DaysPresent = present,
                DaysAbsent = absent,
                PresenceRate = Rate(present, days.Count),
                AbsenceRate = Rate(absent, days.Count),
                DaysLeave = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.Leave),
                DaysUnpaidLeave = days.Count(d => d.IsUnpaidLeave),
                WorkMissionDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.WorkMission),
                PermittedExitCount = days.Count(d => d.HasPermittedExit),
                LateCount = days.Count(d => d.IsLate),
                TotalLateMinutes = days.Sum(d => d.LateMinutes),
                TotalWorkedHours = Math.Round(days.Sum(d => d.WorkedHours), 2)
            };
            stats.Employees.Add(row);

            foreach (var d in days)
            {
                var agg = daily[d.Date];
                agg.Counted++;
                if (d.IsPresent) agg.Present++;
                else agg.Absent++;
                if (d.IsLate) agg.Late++;
            }
        }

        foreach (var d in daily.Values)
            d.PresenceRate = Rate(d.Present, d.Counted);
        stats.Daily = daily.Values.OrderBy(d => d.Date).ToList();

        stats.Groups = stats.Employees
            .GroupBy(e => e.GroupName)
            .Select(g => new GroupStatsRow
            {
                GroupName = g.Key,
                EmployeeCount = g.Count(),
                AvgPresenceRate = Math.Round(g.Average(e => e.PresenceRate), 1),
                TotalAbsentDays = g.Sum(e => e.DaysAbsent),
                TotalLateCount = g.Sum(e => e.LateCount),
                TotalLateMinutes = g.Sum(e => e.TotalLateMinutes)
            })
            .OrderBy(g => g.GroupName)
            .ToList();

        stats.Kpis = new StatsKpis
        {
            EmployeeCount = stats.Employees.Count,
            AvgPresenceRate = stats.Employees.Count > 0
                ? Math.Round(stats.Employees.Average(e => e.PresenceRate), 1) : 0,
            TotalAbsentDays = stats.Employees.Sum(e => e.DaysAbsent),
            TotalLeaveDays = stats.Employees.Sum(e => e.DaysLeave),
            TotalUnpaidLeaveDays = stats.Employees.Sum(e => e.DaysUnpaidLeave),
            TotalMissionDays = stats.Employees.Sum(e => e.WorkMissionDays),
            TotalPermittedExits = stats.Employees.Sum(e => e.PermittedExitCount),
            TotalLateCount = stats.Employees.Sum(e => e.LateCount),
            TotalLateMinutes = stats.Employees.Sum(e => e.TotalLateMinutes)
        };

        // الافتراضي: الأعلى غيابًا أولًا (الأهم للمتابعة)
        stats.Employees = stats.Employees
            .OrderByDescending(e => e.AbsenceRate).ThenBy(e => e.FullName).ToList();

        return stats;
    }

    private static decimal Rate(int part, int total) =>
        total > 0 ? Math.Round(part * 100m / total, 1) : 0;

    public byte[] ExportCsv(StatisticsViewModel stats)
    {
        using var ms = new MemoryStream();
        // BOM لإظهار العربية بشكل صحيح في Excel
        ms.Write(Encoding.UTF8.GetPreamble());
        using (var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteField("الموظف");
            csv.WriteField("الرقم المالي");
            csv.WriteField("المجموعة");
            csv.WriteField("أيام الفترة");
            csv.WriteField("الحضور");
            csv.WriteField("الغياب");
            csv.WriteField("نسبة الحضور %");
            csv.WriteField("نسبة الغياب %");
            csv.WriteField("إجازات");
            csv.WriteField("إجازة بدون مرتب");
            csv.WriteField("مهام عمل");
            csv.WriteField("أذونات خروج");
            csv.WriteField("مرات التأخير");
            csv.WriteField("دقائق التأخير");
            csv.WriteField("ساعات العمل");
            csv.NextRecord();

            foreach (var r in stats.Employees)
            {
                csv.WriteField(r.FullName);
                csv.WriteField(r.FinancialNo);
                csv.WriteField(r.GroupName);
                csv.WriteField(r.PeriodDays);
                csv.WriteField(r.DaysPresent);
                csv.WriteField(r.DaysAbsent);
                csv.WriteField(r.PresenceRate);
                csv.WriteField(r.AbsenceRate);
                csv.WriteField(r.DaysLeave);
                csv.WriteField(r.DaysUnpaidLeave);
                csv.WriteField(r.WorkMissionDays);
                csv.WriteField(r.PermittedExitCount);
                csv.WriteField(r.LateCount);
                csv.WriteField(r.TotalLateMinutes);
                csv.WriteField(r.TotalWorkedHours);
                csv.NextRecord();
            }
        }
        return ms.ToArray();
    }

    public byte[] ExportXlsx(StatisticsViewModel stats)
    {
        using var wb = new XLWorkbook();

        // ورقة 1: إحصائيات الموظفين
        var ws = wb.Worksheets.Add("الموظفون");
        ws.RightToLeft = true;
        string[] empHeaders = { "الموظف", "الرقم المالي", "المجموعة", "أيام الفترة", "الحضور", "الغياب",
            "نسبة الحضور %", "نسبة الغياب %", "إجازات", "إجازة بدون مرتب", "مهام عمل",
            "أذونات خروج", "مرات التأخير", "دقائق التأخير", "ساعات العمل" };
        WriteHeaders(ws, empHeaders);
        var row = 2;
        foreach (var r in stats.Employees)
        {
            ws.Cell(row, 1).Value = r.FullName;
            ws.Cell(row, 2).Value = r.FinancialNo;
            ws.Cell(row, 3).Value = r.GroupName;
            ws.Cell(row, 4).Value = r.PeriodDays;
            ws.Cell(row, 5).Value = r.DaysPresent;
            ws.Cell(row, 6).Value = r.DaysAbsent;
            ws.Cell(row, 7).Value = r.PresenceRate;
            ws.Cell(row, 8).Value = r.AbsenceRate;
            ws.Cell(row, 9).Value = r.DaysLeave;
            ws.Cell(row, 10).Value = r.DaysUnpaidLeave;
            ws.Cell(row, 11).Value = r.WorkMissionDays;
            ws.Cell(row, 12).Value = r.PermittedExitCount;
            ws.Cell(row, 13).Value = r.LateCount;
            ws.Cell(row, 14).Value = r.TotalLateMinutes;
            ws.Cell(row, 15).Value = r.TotalWorkedHours;
            row++;
        }
        ws.Columns().AdjustToContents();

        // ورقة 2: الحضور اليومي
        var wsD = wb.Worksheets.Add("الحضور اليومي");
        wsD.RightToLeft = true;
        WriteHeaders(wsD, new[] { "التاريخ", "اليوم", "عطلة رسمية", "حاضرون", "غائبون", "متأخرون", "المحسوبون", "نسبة الحضور %" });
        row = 2;
        foreach (var d in stats.Daily)
        {
            wsD.Cell(row, 1).Value = d.Date.ToString("yyyy-MM-dd");
            wsD.Cell(row, 2).Value = d.DayName;
            wsD.Cell(row, 3).Value = d.IsHoliday ? "نعم" : "";
            wsD.Cell(row, 4).Value = d.Present;
            wsD.Cell(row, 5).Value = d.Absent;
            wsD.Cell(row, 6).Value = d.Late;
            wsD.Cell(row, 7).Value = d.Counted;
            wsD.Cell(row, 8).Value = d.PresenceRate;
            row++;
        }
        wsD.Columns().AdjustToContents();

        // ورقة 3: المجموعات
        var wsG = wb.Worksheets.Add("المجموعات");
        wsG.RightToLeft = true;
        WriteHeaders(wsG, new[] { "المجموعة", "عدد الموظفين", "متوسط نسبة الحضور %", "إجمالي الغياب", "مرات التأخير", "دقائق التأخير" });
        row = 2;
        foreach (var g in stats.Groups)
        {
            wsG.Cell(row, 1).Value = g.GroupName;
            wsG.Cell(row, 2).Value = g.EmployeeCount;
            wsG.Cell(row, 3).Value = g.AvgPresenceRate;
            wsG.Cell(row, 4).Value = g.TotalAbsentDays;
            wsG.Cell(row, 5).Value = g.TotalLateCount;
            wsG.Cell(row, 6).Value = g.TotalLateMinutes;
            row++;
        }
        wsG.Columns().AdjustToContents();

        // ورقة 4: المؤشرات العامة
        var wsK = wb.Worksheets.Add("المؤشرات");
        wsK.RightToLeft = true;
        var k = stats.Kpis;
        var kpiRows = new (string Label, object Value)[]
        {
            ("الفترة", $"{stats.From:yyyy-MM-dd} — {stats.To:yyyy-MM-dd}"),
            ("عدد الموظفين", k.EmployeeCount),
            ("متوسط نسبة الحضور %", k.AvgPresenceRate),
            ("إجمالي أيام الغياب", k.TotalAbsentDays),
            ("إجمالي الإجازات", k.TotalLeaveDays),
            ("إجمالي الإجازة بدون مرتب", k.TotalUnpaidLeaveDays),
            ("إجمالي مهام العمل", k.TotalMissionDays),
            ("إجمالي أذونات الخروج", k.TotalPermittedExits),
            ("إجمالي مرات التأخير", k.TotalLateCount),
            ("إجمالي دقائق التأخير", k.TotalLateMinutes)
        };
        row = 1;
        foreach (var (label, value) in kpiRows)
        {
            wsK.Cell(row, 1).Value = label;
            wsK.Cell(row, 1).Style.Font.Bold = true;
            wsK.Cell(row, 2).Value = value?.ToString();
            row++;
        }
        wsK.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
    }
}
