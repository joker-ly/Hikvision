namespace Hikvision.Web.Services.Audit;

/// <summary>يسجّل العمليات في النظام مع اسم المستخدم والتاريخ والوقت.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string? details = null, CancellationToken ct = default);
}
