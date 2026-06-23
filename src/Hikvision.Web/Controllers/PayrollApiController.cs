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
        if (CheckApiKey() is { } keyErr) return keyErr;
        if (!ResolveRange(month, year, from, to, out var fromDate, out var toDate, out var rangeErr))
            return BadRequest(new { error = rangeErr });

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
                r.FinancialNo,
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

    // واجهة مبسّطة للمنظومة المالية: الاسم + رقم المنظومة + إجمالي أيام الحضور (شاملة الإجازة)
    // GET /api/payroll/financial?month=3
    [HttpGet("financial")]
    public async Task<IActionResult> Financial(
        [FromQuery] int? month, [FromQuery] int? year,
        [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int? groupId, CancellationToken ct)
    {
        if (CheckApiKey() is { } keyErr) return keyErr;
        if (!ResolveRange(month, year, from, to, out var fromDate, out var toDate, out var rangeErr))
            return BadRequest(new { error = rangeErr });

        var report = await _reports.BuildAsync(groupId, fromDate, toDate, ct);

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = toDate.ToString("yyyy-MM-dd"),
            count = report.Rows.Count,
            employees = report.Rows.Select(r => new
            {
                name = r.FullName,
                financialNo = r.FinancialNo,
                // أيام الحضور تشمل الإجازات والعطل والمهام (كل ما يُحتسب حضورًا مدفوعًا)
                attendanceDays = r.DaysPresent
            })
        });
    }

    private IActionResult? CheckApiKey()
    {
        var configuredKey = _config["Api:Key"];
        if (string.IsNullOrEmpty(configuredKey)) return null;
        var provided = Request.Headers["X-Api-Key"].ToString();
        return string.Equals(provided, configuredKey, StringComparison.Ordinal)
            ? null
            : Unauthorized(new { error = "مفتاح API غير صحيح أو مفقود (X-Api-Key)." });
    }

    private bool ResolveRange(int? month, int? year, string? from, string? to,
        out DateOnly fromDate, out DateOnly toDate, out string? error)
    {
        fromDate = default; toDate = default; error = null;

        if (month.HasValue)
        {
            if (month < 1 || month > 12) { error = "رقم الشهر يجب أن يكون بين 1 و 12."; return false; }
            var y = year ?? _clock.Now.Year;
            fromDate = new DateOnly(y, month.Value, 1);
            toDate = new DateOnly(y, month.Value, DateTime.DaysInMonth(y, month.Value));
            return true;
        }

        if (!DateOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate) ||
            !DateOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
        {
            error = "أرسل month (1-12) أو from و to بصيغة yyyy-MM-dd.";
            return false;
        }
        if (toDate < fromDate) { error = "to يجب أن يكون بعد from."; return false; }
        return true;
    }
}

