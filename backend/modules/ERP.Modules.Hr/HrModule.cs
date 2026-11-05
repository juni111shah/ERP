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
        PermissionDefinition.Of(P.LeaveRead, "View leave requests, balances and the holiday calendar"),
        PermissionDefinition.Of(P.LeaveRequest, "Create, submit and cancel leave requests"),
        PermissionDefinition.Of(P.LeaveApprove, "Approve or reject leave requests; manage leave types and public holidays"),
        PermissionDefinition.Of(P.LeaveManage, "File leave for any employee"),
        PermissionDefinition.Of(P.SalaryRead, "See salary and pay details"),
        PermissionDefinition.Of(P.RecruitmentRead, "View vacancies and candidates"),
        PermissionDefinition.Of(P.RecruitmentManage, "Manage vacancies and candidates, and hire"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("hr.my", "My HR", "/hr/my", "user", null, 5, "hr", "HR and payroll", 400),
        new("hr.departments", "Departments", "/hr/departments", "apartment", P.EmployeesRead, 12, "hr", "HR and payroll", 400),
        new("hr.positions", "Positions", "/hr/positions", "audit", P.EmployeesRead, 14, "hr", "HR and payroll", 400),
        new("hr.orgchart", "Organisation", "/hr/org-chart", "cluster", P.EmployeesRead, 16, "hr", "HR and payroll", 400),
        new("hr.documentexpiry", "Document expiry", "/hr/documents-expiry", "warning", P.EmployeesRead, 18, "hr", "HR and payroll", 400),
        new("hr.loans", "Loans and advances", "/hr/loans", "dollar", P.PayrollRead, 35, "hr", "HR and payroll", 400),
        new("hr.reports", "HR reports", "/hr/reports", "bar-chart", P.EmployeesRead, 70, "hr", "HR and payroll", 400),
        new("hr.dailyattendance", "Daily attendance", "/hr/daily-attendance", "check-square", P.AttendanceRead, 22, "hr", "HR and payroll", 400),
        new("hr.shifts", "Shifts", "/hr/shifts", "clock-circle", P.AttendanceRead, 23, "hr", "HR and payroll", 400),
        new("hr.statutory", "Statutory rules", "/hr/statutory-rules", "audit", P.PayrollRead, 38, "hr", "HR and payroll", 400),
        new("hr.recruitment", "Recruitment", "/hr/recruitment", "team", P.RecruitmentRead, 42, "hr", "HR and payroll", 400),
        new("hr.checklists", "Checklists", "/hr/checklists", "form", P.EmployeesRead, 46, "hr", "HR and payroll", 400),
        new("hr.tasks", "HR tasks", "/hr/tasks", "check-square", P.EmployeesRead, 47, "hr", "HR and payroll", 400),
        new("hr.employees", "Employees", "/hr/employees", "team", P.EmployeesRead, 10, "hr", "HR and payroll", 400),
        new("hr.attendance", "Attendance", "/hr/attendance", "check-square", P.AttendanceRead, 20, "hr", "HR and payroll", 400),
        new("hr.payroll", "Payroll", "/hr/payroll", "wallet", P.PayrollRead, 30, "hr", "HR and payroll", 400),
        new("hr.reviews", "Performance reviews", "/hr/reviews", "fund", P.ReviewsRead, 40, "hr", "HR and payroll", 400),
        new("hr.reviewcycles", "Review cycles", "/hr/review-cycles", "form", P.ReviewsRead, 45, "hr", "HR and payroll", 400),
        new("hr.leave", "Leave requests", "/hr/leave", "check-square", P.LeaveRead, 50, "hr", "HR and payroll", 400),
        new("hr.leavebalances", "Leave balances", "/hr/leave-balances", "bar-chart", P.LeaveRead, 55, "hr", "HR and payroll", 400),
        new("hr.leavetypes", "Leave types", "/hr/leave-types", "form", P.LeaveRead, 60, "hr", "HR and payroll", 400),
        new("hr.holidays", "Public holidays", "/hr/holidays", "book", P.LeaveRead, 65, "hr", "HR and payroll", 400),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("hr.payrollRequiresApproval", "Payroll runs require approval", "false", "bool"),
        new("hr.daysBasis", "Days in a pay month (absence and overtime basis)", "30"),
        new("hr.overtimeMultiplier", "Overtime pay multiplier", "1.25"),
        new("hr.hoursPerDay", "Working hours per day", "8"),
        new("hr.employerEstablishmentId", "WPS employer establishment ID (EID)", "\"\"", "text", "Used in the SCR record of the WPS SIF file."),
        new("hr.weekendDays", "Weekend days (not counted as leave)", "\"Saturday,Sunday\"", "text", "Comma-separated day names, for example Friday,Saturday."),
        new("hr.employerBankRoutingCode", "WPS employer bank routing code", "\"\"", "text", "Routing code of the bank that pays the salaries."),
        new("hr.blockSelfApproval", "Block approving your own leave request", "true", "bool", "When on, nobody can approve or reject a leave request they raised or that is their own."),
        new("hr.noticeShortfallDeduction", "Deduct unserved notice at termination", "false", "bool", "When on, the final settlement deducts the daily rate for each day of the notice period that was not served."),
        new("hr.payrollSeparationOfDuties", "Payroll separation of duties", "false", "bool", "When on, whoever generated or adjusted a payroll run cannot post it, and whoever posted it cannot pay it."),
        new("hr.shiftStart", "Shift start time (late arrivals count after it)", "\"08:00\"", "text", "24-hour time, for example 08:00."),
        new("hr.lateGraceMinutes", "Late arrival grace (minutes)", "10", "int", "Arriving within this many minutes of the shift start is not late."),
        new("hr.wps.columns", "Salary file columns (generic CSV)", "\"\"", "text", "Used when no country format exists. Comma-separated fields, for example employeeNumber,name,iban,bankRoutingCode,labourCard,netPay."),
        new("hr.wps.header", "Salary file header line (generic CSV)", "\"\"", "text", "Optional first line of the generic salary file."),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        [new(Docs.Employee, "Employee", typeof(Employee), Key, P.EmployeesRead)];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("hr-only", "HR and payroll", "Employees, attendance and payroll.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Hr manager", "Employees, attendance and payroll",
                    [P.EmployeesRead, P.EmployeesManage, P.AttendanceRead, P.AttendanceManage, P.PayrollRead, P.PayrollManage, P.PayrollPost, P.PayrollPay, P.ReviewsRead, P.ReviewsManage, P.LeaveRead, P.LeaveRequest, P.LeaveApprove, P.LeaveManage, P.SalaryRead, P.RecruitmentRead, P.RecruitmentManage,
                     CorePermissions.ReportsRead, CorePermissions.EmailSend, CorePermissions.BankRead, CorePermissions.SensitiveRead], Key),
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
        services.AddHrPeople();
        services.AddScoped<ERP.Modules.Hr.Wps.IWpsFileBuilder, ERP.Modules.Hr.Wps.UaeWpsFileBuilder>();
        services.AddScoped<ERP.Modules.Hr.Wps.IWpsFileBuilder, ERP.Modules.Hr.Wps.QatarWpsFileBuilder>();
        services.AddScoped<GratuityCalculator>();
        services.AddScoped<GratuityProvision>();
        services.AddScoped<StatutoryEngine>();
        services.AddScoped<EmployeeHistoryRecorder>();
        services.AddScoped<ERP.Application.Reminders.IReminderSource, EmployeeContractReminders>();
        services.AddScoped<LeaveCalendarProvider>();
        services.AddScoped<LeaveFeed>();
        services.AddScoped<LeavePolicyService>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<LeaveRequest>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), LeavePolicyService.AttachmentOwnerType, P.LeaveRead, P.LeaveRequest));
        services.AddScoped<IDashboardContributor, HrDashboard>();
        services.AddScoped<ERP.Application.Reminders.IReminderSource, EmployeeDocumentReminders>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<EmployeeDocument>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "hr.employeedocument", P.EmployeesRead, P.EmployeesManage));
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
        mb.Entity<EmployeeDocument>(b =>
        {
            b.ToTable("hr_EmployeeDocuments");
            b.Property(x => x.Number).HasMaxLength(100);
            b.Property(x => x.IssuedBy).HasMaxLength(150);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId });
            b.HasIndex(x => new { x.OrgId, x.ExpiryDate });
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
        mb.Entity<LeaveType>(b =>
        {
            b.ToTable("hr_LeaveTypes");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.AnnualDays).HasPrecision(9, 2);
            b.Property(x => x.CarryForwardCap).HasPrecision(9, 2);
            b.HasMany(x => x.Tiers).WithOne().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.PayBands).WithOne().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<LeaveEntitlementTier>(b =>
        {
            b.ToTable("hr_LeaveEntitlementTiers");
            b.Property(x => x.FromServiceYears).HasPrecision(9, 2);
            b.Property(x => x.Days).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.LeaveTypeId });
        });
        mb.Entity<LeavePayBand>(b =>
        {
            b.ToTable("hr_LeavePayBands");
            b.Property(x => x.PayPercent).HasPrecision(5, 2);
            b.HasIndex(x => new { x.OrgId, x.LeaveTypeId });
        });
        mb.Entity<LeaveRequestYearDays>(b =>
        {
            b.ToTable("hr_LeaveRequestYearDays");
            b.Property(x => x.Days).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.RequestId });
        });
        mb.Entity<LeaveBalance>(b =>
        {
            b.ToTable("hr_LeaveBalances");
            b.Property(x => x.Entitled).HasPrecision(9, 2);
            b.Property(x => x.Carried).HasPrecision(9, 2);
            b.Property(x => x.Taken).HasPrecision(9, 2);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<LeaveRequest>(b =>
        {
            b.ToTable("hr_LeaveRequests");
            b.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.DecisionComment).HasMaxLength(500);
            b.Property(x => x.Days).HasPrecision(9, 2);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.FromDate });
            b.HasIndex(x => new { x.OrgId, x.Status });
        });
        mb.Entity<PublicHoliday>(b =>
        {
            b.ToTable("hr_PublicHolidays");
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
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
            foreach (var m in new[] { nameof(PayrollRun.GrossEarnings), nameof(PayrollRun.Deductions), nameof(PayrollRun.NetPay), nameof(PayrollRun.EmployerCost) }) b.Property(m).HasPrecision(19, 4);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Kind] = 0 AND [Status] <> 4"); // one live Regular run per month
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        StructureModel.Configure(mb);
        mb.Entity<AttendanceLog>(b =>
        {
            b.ToTable("hr_AttendanceLogs");
            b.Property(x => x.Notes).HasMaxLength(300);
            b.Property(x => x.Source).HasMaxLength(50);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Date });
        });
        mb.Entity<EmployeeHistoryEntry>(b =>
        {
            b.ToTable("hr_EmployeeHistory");
            b.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            b.Property(x => x.OldValue).HasMaxLength(300);
            b.Property(x => x.NewValue).HasMaxLength(300);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.EffectiveDate });
        });
        mb.Entity<Shift>(b =>
        {
            b.ToTable("hr_Shifts");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ChecklistTemplate>(b =>
        {
            b.ToTable("hr_ChecklistTemplates");
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ChecklistTemplateItem>(b =>
        {
            b.ToTable("hr_ChecklistTemplateItems");
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.TemplateId });
        });
        mb.Entity<EmployeeTask>(b =>
        {
            b.ToTable("hr_EmployeeTasks");
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId });
            b.HasIndex(x => new { x.OrgId, x.IsDone, x.DueDate });
        });
        mb.Entity<JobOpening>(b =>
        {
            b.ToTable("hr_JobOpenings");
            b.Property(x => x.Title).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
            b.HasIndex(x => new { x.OrgId, x.Status });
        });
        mb.Entity<Candidate>(b =>
        {
            b.ToTable("hr_Candidates");
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.Source).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.ExpectedSalary).HasPrecision(19, 4);
            b.HasOne<JobOpening>().WithMany().HasForeignKey(x => x.OpeningId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.OpeningId, x.Stage });
        });
        mb.Entity<StatutoryRule>(b =>
        {
            b.ToTable("hr_StatutoryRules");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Nationalities).HasMaxLength(300);
            foreach (var m in new[] { nameof(StatutoryRule.Rate), nameof(StatutoryRule.Threshold), nameof(StatutoryRule.Cap) }) b.Property(m).HasPrecision(19, 4);
            b.HasMany(x => x.Slabs).WithOne().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<StatutorySlab>(b =>
        {
            b.ToTable("hr_StatutorySlabs");
            b.Property(x => x.FromAmount).HasPrecision(19, 4);
            b.Property(x => x.Rate).HasPrecision(9, 4);
            b.HasIndex(x => new { x.OrgId, x.RuleId });
        });
        mb.Entity<PayrollLineStatutory>(b =>
        {
            b.ToTable("hr_PayrollLineStatutory");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.BaseAmount).HasPrecision(19, 4);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.PayrollRunId });
            b.HasIndex(x => new { x.OrgId, x.RuleId });
        });
        mb.Entity<EmployeeLoan>(b =>
        {
            b.ToTable("hr_EmployeeLoans");
            b.Property(x => x.Reason).HasMaxLength(300);
            foreach (var m in new[] { nameof(EmployeeLoan.Principal), nameof(EmployeeLoan.InstalmentAmount), nameof(EmployeeLoan.Remaining) }) b.Property(m).HasPrecision(19, 4);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.EmployeeId, x.Status });
        });
        mb.Entity<PayrollLoanRecovery>(b =>
        {
            b.ToTable("hr_PayrollLoanRecoveries");
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.HasOne<EmployeeLoan>().WithMany().HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.PayrollRunId });
            b.HasIndex(x => new { x.OrgId, x.LoanId });
        });
        mb.Entity<PayrollLine>(b =>
        {
            b.ToTable("hr_PayrollLines");
            b.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
            b.Property(x => x.EmployeeNumber).HasMaxLength(30);
            b.Property(x => x.AdjustmentNote).HasMaxLength(300);
            foreach (var m in new[] { nameof(PayrollLine.Basic), nameof(PayrollLine.Allowances), nameof(PayrollLine.Overtime), nameof(PayrollLine.Absence), nameof(PayrollLine.Adjustment), nameof(PayrollLine.StatutoryDeduction), nameof(PayrollLine.EmployerCost), nameof(PayrollLine.LoanRecovery), nameof(PayrollLine.LeaveEncashment), nameof(PayrollLine.EndOfService), nameof(PayrollLine.GrossEarnings), nameof(PayrollLine.Deductions), nameof(PayrollLine.NetPay) }) b.Property(m).HasPrecision(19, 4);
            b.Property(x => x.OvertimeHours).HasPrecision(9, 2);
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => HrEndpoints.Map(group);
}
