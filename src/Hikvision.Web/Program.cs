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

// قاعدة البيانات
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// تطبيق الترحيلات وزرع البيانات الأولية
await DbSeeder.SeedAsync(app.Services, app.Configuration);

app.Run();
