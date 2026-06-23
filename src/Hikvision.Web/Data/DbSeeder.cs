using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // إذا وُجدت ترحيلات نطبّقها، وإلا ننشئ قاعدة البيانات مباشرة (يعمل قبل إضافة أول migration).
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        // 1) مستخدم المدير
        if (!await db.Users.AnyAsync())
        {
            var hasher = new PasswordHasher<AppUser>();
            var user = new AppUser
            {
                Username = config["Admin:Username"] ?? "admin",
                DisplayName = config["Admin:DisplayName"] ?? "مدير النظام",
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, config["Admin:Password"] ?? "ChangeMe123!");
            db.Users.Add(user);
        }

        // 2) المجموعات الافتراضية + الورديات
        if (!await db.EmployeeGroups.AnyAsync())
        {
            var managers = new EmployeeGroup
            {
                Name = "المدراء",
                Description = "غير مقيّدين بوقت — بصمة دخول واحدة تكفي",
                IsTimeBound = false,
                CalculationMode = CalculationMode.ByCheckInOut,
                Schedule = new WorkSchedule
                {
                    Name = "وردية المدراء",
                    StartTime = null,
                    EndTime = null,
                    WorkDaysMask = 0b0011111
                }
            };

            var staff = new EmployeeGroup
            {
                Name = "الموظفون",
                Description = "مقيّدون بحضور وانصراف ضمن مواعيد محددة",
                IsTimeBound = true,
                CalculationMode = CalculationMode.ByCheckInOut,
                Schedule = new WorkSchedule
                {
                    Name = "الوردية الصباحية",
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(16, 0),
                    LateGraceMinutes = 10,
                    RequiredDailyHours = 8m,
                    WorkDaysMask = 0b0011111
                }
            };

            db.EmployeeGroups.AddRange(managers, staff);
        }

        // 3) إعدادات الجهاز
        if (!await db.DeviceConfigs.AnyAsync())
        {
            db.DeviceConfigs.Add(new DeviceConfig
            {
                Host = config["Device:Host"] ?? "192.168.1.64",
                Port = config.GetValue("Device:Port", 80),
                UseHttps = config.GetValue("Device:UseHttps", false),
                Username = config["Device:Username"] ?? "admin",
                Password = config["Device:Password"] ?? string.Empty
            });
        }

        await db.SaveChangesAsync();
    }
}
