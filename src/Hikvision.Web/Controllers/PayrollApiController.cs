using System.Globalization;
using Hikvision.Web.Services.Reports;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hikvision.Web.Controllers;

/// <summary>
/// واجهة برمجية (API) لتصدير بيانات تقرير الحضور والمرتبات.
/// يمكن إرسال رقم الشهر فقط (month=3) فيُحسب من 1/3 إلى نهاية الشهر،
/// أو إرسال from/to صراحةً. الاستجابة JSON محسوبة مباشرة.
/// تُحمى اختياريًا بمفتاح API عبر الإعداد Api:Key (ترويسة X-Api-Key).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/payroll")]
public class PayrollApiController : ControllerBase
{
    private readonly IPayrollReportService _reports;
    private readonly IConfiguration _config;
    private readonly IAppClock _clock;

    public PayrollApiController(IPayrollReportService reports, IConfiguration config, IAppClock clock)
    {
        _reports = reports;
        _config = config;
        _clock = clock;
    }

    // GET /api/payroll?month=3            => من 2026-03-01 إلى 2026-03-31 (السنة الحالية)
    // GET /api/payroll?month=3&year=2025  => تحديد السنة
    // GET /api/payroll?from=2026-06-01&to=2026-06-30
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? month, [FromQuery] int? year,
        [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int? groupId, CancellationToken ct)
    {
        // التحقق من مفتاح API إن كان مضبوطًا
        var configuredKey = _config["Api:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            var provided = Request.Headers["X-Api-Key"].ToString();
            if (!string.Equals(provided, configuredKey, StringComparison.Ordinal))
                return Unauthorized(new { error = "مفتاح API غير صحيح أو مفقود (X-Api-Key)." });
        }

        DateOnly fromDate, toDate;

        if (month.HasValue)
        {
            if (month < 1 || month > 12)
                return BadRequest(new { error = "رقم الشهر يجب أن يكون بين 1 و 12." });

            var y = year ?? _clock.Now.Year;
            fromDate = new DateOnly(y, month.Value, 1);
            toDate = new DateOnly(y, month.Value, DateTime.DaysInMonth(y, month.Value));
        }
        else
        {
            if (!DateOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate) ||
                !DateOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
                return BadRequest(new { error = "أرسل month (1-12) أو from و to بصيغة yyyy-MM-dd." });

            if (toDate < fromDate)
                return BadRequest(new { error = "to يجب أن يكون بعد from." });
        }

        var report = await _reports.BuildAsync(groupId, fromDate, toDate, ct);

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = toDate.ToString("yyyy-MM-dd"),
            groupId,
            count = report.Rows.Count,
            rows = report.Rows.Select(r => new
            {
                r.EmployeeId,
                r.FullName,
                r.GroupName,
                r.DaysPresent,
                r.DaysAbsent,
                r.DaysLeave,
                r.WorkMissionDays,
                r.EarlyExitPermissionCount,
                r.HolidayDays,
                r.LateCount,
                r.TotalLateMinutes,
                r.TotalWorkedHours,
                r.BaseSalary,
                r.EstimatedPay
            })
        });
    }
}

