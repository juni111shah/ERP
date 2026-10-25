using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr;

/// <summary>Employees, monthly attendance exceptions, payroll runs (accrual and payment posted to the ledger), end-of-service gratuity.</summary>
public sealed class HrModule : ModuleBase
{
    public const string Key = "hr";

    public override ModuleManifest Manifest { get; } = new(Key, "HR and payroll", "Employees, attendance, payroll and end-of-service benefits.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.EmployeesRead, "View employees and their salary structure"),
        PermissionDefinition.Of(P.EmployeesManage, "Create and edit employees"),
        PermissionDefinition.Of(P.AttendanceRead, "View monthly attendance"),
        PermissionDefinition.Of(P.AttendanceManage, "Record attendance exceptions"),
        PermissionDefinition.Of(P.PayrollRead, "View payroll runs"),
        PermissionDefinition.Of(P.PayrollManage, "Generate, recalculate and adjust draft payroll runs"),
        PermissionDefinition.Of(P.PayrollPost, "Post payroll and gratuity provisions"),
        PermissionDefinition.Of(P.PayrollPay, "Pay payroll and prepare the bank and WPS files"),
        PermissionDefinition.Of(P.ReviewsRead, "View review cycles and performance reviews"),
        PermissionDefinition.Of(P.ReviewsManage, "Manage review cycles; write, submit and acknowledge performance reviews"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("hr.employees", "Employees", "/hr/employees", "team", P.EmployeesRead, 10, "hr", "HR and payroll", 400),
        new("hr.attendance", "Attendance", "/hr/attendance", "check-square", P.AttendanceRead, 20, "hr", "HR and payroll", 400),
        new("hr.payroll", "Payroll", "/hr/payroll", "wallet", P.PayrollRead, 30, "hr", "HR and payroll", 400),
        new("hr.reviews", "Performance reviews", "/hr/reviews", "fund", P.ReviewsRead, 40, "hr", "HR and payroll", 400),
        new("hr.reviewcycles", "Review cycles", "/hr/review-cycles", "form", P.ReviewsRead, 45, "hr", "HR and payroll", 400),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("hr.payrollRequiresApproval", "Payroll runs require approval", "false", "bool"),
        new("hr.daysBasis", "Days in a pay month (absence and overtime basis)", "30"),
        new("hr.overtimeMultiplier", "Overtime pay multiplier", "1.25"),
        new("hr.hoursPerDay", "Working hours per day", "8"),
        new("hr.employerEstablishmentId", "WPS employer establishment ID (EID)", "\"\"", "text", "Used in the SCR record of the WPS SIF file."),
        new("hr.employerBankRoutingCode", "WPS employer bank routing code", "\"\"", "text", "Routing code of the bank that pays the salaries."),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        [new(Docs.Employee, "Employee", typeof(Employee), Key, P.EmployeesRead)];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("hr-only", "HR and payroll", "Employees, attendance and payroll.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Hr manager", "Employees, attendance and payroll",
                    [P.EmployeesRead, P.EmployeesManage, P.AttendanceRead, P.AttendanceManage, P.PayrollRead, P.PayrollManage, P.PayrollPost, P.PayrollPay, P.ReviewsRead, P.ReviewsManage,
                     CorePermissions.ReportsRead, CorePermissions.BankRead, CorePermissions.SensitiveRead], Key),
            ],
            Sequences:
            [
                new(Docs.Employee, "EMP", "{prefix}-{seq:4}", false),
                new(Docs.Payroll, "PR"),
            ],
            SortOrder: 60),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GratuityCalculator>();
        services.AddScoped<IDashboardContributor, HrDashboard>();
        services.AddScoped<ICashForecastSource, PayrollForecastSource>();
        services.AddScoped<ERP.Application.Search.ISearchProvider, HrSearch>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<Employee>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "employee", P.EmployeesRead, P.EmployeesManage));
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<Employee>(b =>
        {
            b.ToTable("hr_Employees");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Designation).HasMaxLength(150).IsRequired();
            b.Property(x => x.Department).HasMaxLength(100);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.Nationality).HasMaxLength(100);
            b.Property(x => x.Iban).HasMaxLength(50);
            b.Property(x => x.LabourCardNumber).HasMaxLength(30);
            b.Property(x => x.BankRoutingCode).HasMaxLength(20);
            foreach (var m in new[] { nameof(Employee.BasicSalary), nameof(Employee.HousingAllowance), nameof(Employee.TransportAllowance), nameof(Employee.OtherAllowances), nameof(Employee.RecurringDeductions) }) b.Property(m).HasPrecision(19, 4);
            b.Ignore(x => x.GrossMonthly); b.Ignore(x => x.Allowances);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
        });
        mb.Entity<ReviewCycle>(b =>
        {
            b.ToTable("hr_ReviewCycles");
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<PerformanceReview>(b =>
        {
            b.ToTable("hr_PerformanceReviews");
            b.Property(x => x.ReviewerName).HasMaxLength(150);
            b.Property(x => x.Goals).HasMaxLength(4000);
            b.Property(x => x.Comments).HasMaxLength(4000);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Competencies).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.CycleId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ReviewCompetency>(b =>
        {
            b.ToTable("hr_ReviewCompetencies");
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
        });
        mb.Entity<AttendanceRecord>(b =>
        {
            b.ToTable("hr_AttendanceRecords");
            b.Property(x => x.OvertimeHours).HasPrecision(9, 2);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<PayrollRun>(b =>
        {
            b.ToTable("hr_PayrollRuns");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            foreach (var m in new[] { nameof(PayrollRun.GrossEarnings), nameof(PayrollRun.Deductions), nameof(PayrollRun.NetPay) }) b.Property(m).HasPrecision(19, 4);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<PayrollLine>(b =>
        {
            b.ToTable("hr_PayrollLines");
            b.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
            b.Property(x => x.EmployeeNumber).HasMaxLength(30);
            b.Property(x => x.AdjustmentNote).HasMaxLength(300);
            foreach (var m in new[] { nameof(PayrollLine.Basic), nameof(PayrollLine.Allowances), nameof(PayrollLine.Overtime), nameof(PayrollLine.Absence), nameof(PayrollLine.Adjustment), nameof(PayrollLine.GrossEarnings), nameof(PayrollLine.Deductions), nameof(PayrollLine.NetPay) }) b.Property(m).HasPrecision(19, 4);
            b.Property(x => x.OvertimeHours).HasPrecision(9, 2);
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => HrEndpoints.Map(group);
}
