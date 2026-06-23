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

public class PayrollReportService : IPayrollReportService
{
    private readonly AppDbContext _db;
    private readonly IAttendanceCalculationService _calc;

    public PayrollReportService(AppDbContext db, IAttendanceCalculationService calc)
    {
        _db = db;
        _calc = calc;
    }

    public async Task<PayrollReportViewModel> BuildAsync(int? groupId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var report = new PayrollReportViewModel { GroupId = groupId, From = from, To = to };

        var query = _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .Where(e => e.IsActive);
        if (groupId.HasValue)
            query = query.Where(e => e.EmployeeGroupId == groupId.Value);

        var employees = await query.AsNoTracking().ToListAsync(ct);

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var holidays = await _calc.LoadHolidaysAsync(from, to, ct);

        // عدد أيام العمل في الفترة (لحساب الخصم) — يُؤخذ من وردية المجموعة إن وُجدت
        foreach (var emp in employees)
        {
            var records = await _db.AttendanceRecords
                .Where(r => r.EmployeeId == emp.Id && r.EventTime >= fromDt && r.EventTime <= toDt)
                .AsNoTracking().ToListAsync(ct);

            var days = _calc.Calculate(emp.Group!, emp.Group!.Schedule, records, from, to, holidays);

            var workingDays = days.Count(d => d.IsWorkingDay);
            var absentDays = days.Count(d => d.IsAbsent);

            var row = new PayrollRow
            {
                EmployeeId = emp.Id,
                FullName = emp.FullName,
                GroupName = emp.Group!.Name,
                DaysPresent = days.Count(d => d.IsPresent),
                DaysAbsent = absentDays,
                DaysLeave = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.Leave),
                WorkMissionDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.WorkMission),
                EarlyExitPermissionCount = days.Count(d => d.HasPermittedExit),
                HolidayDays = days.Count(d => d.IsHoliday),
                LateCount = days.Count(d => d.IsLate),
                TotalLateMinutes = days.Sum(d => d.LateMinutes),
                TotalWorkedHours = Math.Round(days.Sum(d => d.WorkedHours), 2),
                BaseSalary = emp.BaseSalary
            };

            // تقدير مبدئي للصرف: خصم عن أيام الغياب غير المبرّر
            if (emp.BaseSalary is { } salary && workingDays > 0)
            {
                var perDay = salary / workingDays;
                row.EstimatedPay = Math.Round(Math.Max(0, salary - perDay * absentDays), 2);
            }
            else
            {
                row.EstimatedPay = emp.BaseSalary;
            }

            report.Rows.Add(row);
        }

        return report;
    }

    public byte[] ExportCsv(PayrollReportViewModel report)
    {
        using var ms = new MemoryStream();
        // BOM لإظهار العربية بشكل صحيح في Excel
        ms.Write(Encoding.UTF8.GetPreamble());
        using (var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteField("الموظف");
            csv.WriteField("المجموعة");
            csv.WriteField("أيام الحضور");
            csv.WriteField("أيام الغياب");
            csv.WriteField("أيام الإجازة");
            csv.WriteField("أيام مهام العمل");
            csv.WriteField("أذونات الخروج المبكر");
            csv.WriteField("أيام العطل الرسمية");
            csv.WriteField("مرات التأخير");
            csv.WriteField("دقائق التأخير");
            csv.WriteField("إجمالي ساعات العمل");
            csv.WriteField("الراتب الأساسي");
            csv.WriteField("تقدير الصرف");
            csv.NextRecord();

            foreach (var r in report.Rows)
            {
                csv.WriteField(r.FullName);
                csv.WriteField(r.GroupName);
                csv.WriteField(r.DaysPresent);
                csv.WriteField(r.DaysAbsent);
                csv.WriteField(r.DaysLeave);
                csv.WriteField(r.WorkMissionDays);
                csv.WriteField(r.EarlyExitPermissionCount);
                csv.WriteField(r.HolidayDays);
                csv.WriteField(r.LateCount);
                csv.WriteField(r.TotalLateMinutes);
                csv.WriteField(r.TotalWorkedHours);
                csv.WriteField(r.BaseSalary);
                csv.WriteField(r.EstimatedPay);
                csv.NextRecord();
            }
        }
        return ms.ToArray();
    }

    public byte[] ExportXlsx(PayrollReportViewModel report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("المرتبات");
        ws.RightToLeft = true;

        string[] headers = { "الموظف", "المجموعة", "أيام الحضور", "أيام الغياب", "أيام الإجازة",
            "أيام مهام العمل", "أذونات الخروج المبكر", "أيام العطل الرسمية",
            "مرات التأخير", "دقائق التأخير", "إجمالي ساعات العمل", "الراتب الأساسي", "تقدير الصرف" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.FullName;
            ws.Cell(row, 2).Value = r.GroupName;
            ws.Cell(row, 3).Value = r.DaysPresent;
            ws.Cell(row, 4).Value = r.DaysAbsent;
            ws.Cell(row, 5).Value = r.DaysLeave;
            ws.Cell(row, 6).Value = r.WorkMissionDays;
            ws.Cell(row, 7).Value = r.EarlyExitPermissionCount;
            ws.Cell(row, 8).Value = r.HolidayDays;
            ws.Cell(row, 9).Value = r.LateCount;
            ws.Cell(row, 10).Value = r.TotalLateMinutes;
            ws.Cell(row, 11).Value = r.TotalWorkedHours;
            if (r.BaseSalary.HasValue) ws.Cell(row, 12).Value = r.BaseSalary.Value;
            if (r.EstimatedPay.HasValue) ws.Cell(row, 13).Value = r.EstimatedPay.Value;
            row++;
        }
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
