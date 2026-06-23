using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.TimeZoneSupport;

namespace Hikvision.Web.Services.Audit;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;
    private readonly IHttpContextAccessor _http;

    public AuditLogger(AppDbContext db, IAppClock clock, IHttpContextAccessor http)
    {
        _db = db;
        _clock = clock;
        _http = http;
    }

    public async Task LogAsync(string action, string? details = null, CancellationToken ct = default)
    {
        var user = _http.HttpContext?.User?.Identity?.Name ?? "نظام";
        _db.AuditLogs.Add(new AuditLog
        {
            UserName = user,
            Action = action,
            Details = details,
            TimestampLocal = _clock.Now
        });
        await _db.SaveChangesAsync(ct);
    }
}
