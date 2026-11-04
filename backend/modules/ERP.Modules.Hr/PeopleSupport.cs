using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr;

/// <summary>Registrations for the people features (employee links, structure, self-service, payroll separation of duties).</summary>
public static class PeopleServices
{
    public static IServiceCollection AddHrPeople(this IServiceCollection services)
    {
        services.AddScoped<EmployeeDtoFactory>();
        services.AddScoped<EmployeePeople>();
        services.AddScoped<PayrollSeparation>();
        return services;
    }
}

/// <summary>Model configuration of the people data (columns on Employee and PayrollRun, Department, Position).</summary>
internal static class StructureModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<Employee>(b =>
        {
            b.Property(x => x.Gender).HasMaxLength(30);
            b.Property(x => x.MaritalStatus).HasMaxLength(30);
            b.Property(x => x.EmergencyContactName).HasMaxLength(150);
            b.Property(x => x.EmergencyContactPhone).HasMaxLength(50);
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.BankName).HasMaxLength(150);
            b.Property(x => x.TerminationReason).HasMaxLength(300);
            b.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.ManagerEmployeeId });
            b.HasIndex(x => new { x.OrgId, x.DepartmentId });
        });
        mb.Entity<Department>(b =>
        {
            b.ToTable("hr_Departments");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<Position>(b =>
        {
            b.ToTable("hr_Positions");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Title).HasMaxLength(150).IsRequired();
            b.Property(x => x.Grade).HasMaxLength(50);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
    }
}

/// <summary>Optional separation of duties on payroll (setting <c>hr.payrollSeparationOfDuties</c>): whoever prepared a run cannot post it, whoever posted it cannot pay it. Stamps who did what.</summary>
public sealed class PayrollSeparation(ICurrentUser user, ISettingsService settings)
{
    public void StampGenerated(PayrollRun run) { run.GeneratedByUserId = user.UserId; run.LastAdjustedByUserId = null; }
    public void StampAdjusted(PayrollRun run) => run.LastAdjustedByUserId = user.UserId;
    public void StampPosted(PayrollRun run) => run.PostedByUserId = user.UserId;

    public async Task RequireCanPostAsync(PayrollRun run, CancellationToken ct)
    {
        if (!await EnabledAsync(run, ct) || user.UserId is not { } me) return;
        if (run.GeneratedByUserId == me || run.LastAdjustedByUserId == me)
            throw new ConflictException("Separation of duties: you generated or adjusted this payroll, so another user must post it.");
    }

    public async Task RequireCanPayAsync(PayrollRun run, CancellationToken ct)
    {
        if (!await EnabledAsync(run, ct) || user.UserId is not { } me) return;
        if (run.PostedByUserId == me)
            throw new ConflictException("Separation of duties: you posted this payroll, so another user must pay it.");
    }

    private async Task<bool> EnabledAsync(PayrollRun run, CancellationToken ct)
        => await settings.GetAsync<bool?>("hr.payrollSeparationOfDuties", run.LegalEntityId, ct) == true;
}
