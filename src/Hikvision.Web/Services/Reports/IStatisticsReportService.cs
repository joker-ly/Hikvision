using Hikvision.Web.ViewModels.Reports;

namespace Hikvision.Web.Services.Reports;

public interface IStatisticsReportService
{
    /// <summary>بناء إحصائيات الفترة (لكل موظف + يومي + مجموعات + مؤشرات عامة).</summary>
    Task<StatisticsViewModel> BuildAsync(int? groupId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>تصدير جدول إحصائيات الموظفين CSV (بترميز يدعم العربية في Excel).</summary>
    byte[] ExportCsv(StatisticsViewModel stats);

    /// <summary>تصدير مصنّف Excel متعدد الأوراق (موظفون/يومي/مجموعات/مؤشرات).</summary>
    byte[] ExportXlsx(StatisticsViewModel stats);
}
