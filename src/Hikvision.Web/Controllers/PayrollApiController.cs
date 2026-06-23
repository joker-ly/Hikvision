using System.Globalization;
using Hikvision.Web.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hikvision.Web.Controllers;

/// <summary>
/// واجهة برمجية (API) لتصدير بيانات تقرير الحضور والمرتبات.
/// الطلب يرسل التاريخ من/إلى فقط، والاستجابة تحسب مباشرة وتعيد JSON.
/// تُحمى اختياريًا بمفتاح API عبر الإعداد Api:Key (ترويسة X-Api-Key).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/payroll")]
public class PayrollApiController : ControllerBase
{
    private readonly IPayrollReportService _reports;
    private readonly IConfiguration _config;

    public PayrollApiController(IPayrollReportService reports, IConfiguration config)
    {
        _reports = reports;
        _config = config;
    }

    // GET /api/payroll?from=2026-06-01&to=2026-06-30&groupId=2
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string from, [FromQuery] string to,
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

        if (!DateOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            return BadRequest(new { error = "صيغة التاريخ غير صحيحة. استخدم yyyy-MM-dd للمعاملين from و to." });

        if (toDate < fromDate)
            return BadRequest(new { error = "to يجب أن يكون بعد from." });

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
