using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.Calculation;
using Hikvision.Web.Services.Hikvision;
using Hikvision.Web.Services.Reports;
using Hikvision.Web.Services.Sync;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// دعم التشغيل كخدمة Windows (تبدأ مع إقلاع النظام). لا تأثير على المنصات الأخرى.
builder.Host.UseWindowsService();

// قاعدة البيانات — سلسلة الاتصال تُقرأ من المزوّد (الملف المحفوظ أو appsettings)
// لتمكين معالج الإعداد عند أول تشغيل. الخيارات Scoped فتُقرأ القيمة الحالية لكل نطاق.
builder.Services.AddSingleton<Hikvision.Web.Services.Setup.DbConnectionStringProvider>();
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    opt.UseSqlServer(
        sp.GetRequiredService<Hikvision.Web.Services.Setup.DbConnectionStringProvider>().Current
        ?? "Server=.;Database=_unconfigured_;Trusted_Connection=True;TrustServerCertificate=True"));

// المصادقة بالكوكيز (مدير واحد) + إلزام تسجيل الدخول على كل الصفحات
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews(o =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.Filters.Add(new AuthorizeFilter(policy));
});

// الخدمات
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddSingleton<PasswordHasher<AppUser>>();

// اختيار عميل الجهاز: حقيقي أو وهمي حسب الإعداد
if (builder.Configuration.GetValue("Device:UseFakeClient", false))
    builder.Services.AddScoped<IHikvisionIsapiClient, FakeHikvisionIsapiClient>();
else
    builder.Services.AddScoped<IHikvisionIsapiClient, HikvisionIsapiClient>();

builder.Services.AddScoped<IAttendanceSyncService, AttendanceSyncService>();
builder.Services.AddScoped<IAttendanceCalculationService, AttendanceCalculationService>();
builder.Services.AddScoped<IPayrollReportService, PayrollReportService>();
builder.Services.AddScoped<IStatisticsReportService, StatisticsReportService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Hikvision.Web.Services.Audit.IAuditLogger, Hikvision.Web.Services.Audit.AuditLogger>();

// المزامنة المجدولة اليومية (خدمة خلفية)
builder.Services.AddHostedService<Hikvision.Web.Services.Sync.ScheduledSyncService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// بوابة الإعداد: قبل اكتمال تهيئة القاعدة، توجَّه كل الطلبات إلى معالج الإعداد /Setup
// (الملفات الثابتة تُخدَم قبل هذه النقطة فلا تتأثر).
app.Use(async (ctx, next) =>
{
    var provider = ctx.RequestServices.GetRequiredService<Hikvision.Web.Services.Setup.DbConnectionStringProvider>();
    if (!provider.IsReady)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/Setup", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Redirect("/Setup");
            return;
        }
    }
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// محاولة تهيئة القاعدة عند الإقلاع؛ عند الفشل لا يتعطّل التطبيق بل يُعرض معالج الإعداد.
var dbProvider = app.Services.GetRequiredService<Hikvision.Web.Services.Setup.DbConnectionStringProvider>();
try
{
    await DbSeeder.SeedAsync(app.Services, app.Configuration);
    dbProvider.MarkReady();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "تعذّرت تهيئة قاعدة البيانات عند الإقلاع — سيُعرض معالج الإعداد.");
}

app.Run();
