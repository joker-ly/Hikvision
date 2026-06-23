using Hikvision.Web.ViewModels.Reports;

namespace Hikvision.Web.Services.Reports;

public interface IPayrollReportService
{
    Task<PayrollReportViewModel> BuildAsync(int? groupId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>تصدير التقرير كـ CSV (UTF-8 مع BOM لإظهار العربية في Excel).</summary>
    byte[] ExportCsv(PayrollReportViewModel report);

    /// <summary>تصدير التقرير كملف Excel (xlsx).</summary>
    byte[] ExportXlsx(PayrollReportViewModel report);
}
