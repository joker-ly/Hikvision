using Hikvision.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<EmployeeGroup> EmployeeGroups => Set<EmployeeGroup>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>()
            .HasIndex(u => u.Username).IsUnique();

        b.Entity<EmployeeGroup>()
            .HasMany(g => g.Employees)
            .WithOne(e => e.Group!)
            .HasForeignKey(e => e.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // علاقة 1:1 بين المجموعة والوردية
        b.Entity<EmployeeGroup>()
            .HasOne(g => g.Schedule)
            .WithOne(s => s.Group!)
            .HasForeignKey<WorkSchedule>(s => s.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WorkSchedule>()
            .HasIndex(s => s.EmployeeGroupId).IsUnique();

        b.Entity<Employee>()
            .HasIndex(e => e.DeviceEmployeeNo).IsUnique();

        b.Entity<Employee>()
            .Property(e => e.BaseSalary).HasColumnType("decimal(18,2)");

        b.Entity<WorkSchedule>()
            .Property(s => s.RequiredDailyHours).HasColumnType("decimal(5,2)");

        b.Entity<AttendanceRecord>()
            .HasOne(r => r.Employee)
            .WithMany(e => e.Records)
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // منع التكرار: نفس البصمة لنفس الموظف في نفس الوقت والاتجاه والمصدر
        b.Entity<AttendanceRecord>()
            .HasIndex(r => new { r.EmployeeId, r.EventTime, r.Direction, r.Source }).IsUnique();

        b.Entity<AttendanceRecord>()
            .HasIndex(r => new { r.EmployeeId, r.EventTime });
    }
}
