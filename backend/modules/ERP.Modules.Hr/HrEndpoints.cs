using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Hr;

public sealed record AdjustLineBody(decimal Adjustment, string? Note);
public sealed record PayrollPayBody(Guid BankAccountId, DateOnly Date);
public sealed record ProvisionBody(Guid LegalEntityId, DateOnly AsOf);

/// <summary>Thin controllers: every endpoint sends one MediatR request.</summary>
internal static class HrEndpoints
{
    private const string Base = "/api/v1/m/hr";

    public static void Map(RouteGroupBuilder group)
    {
        var employees = group.MapGroup("/employees");
        employees.MapPagedList<Employee, EmployeeSummaryDto>("", P.EmployeesRead, e => new EmployeeSummaryDto(e.Id, e.Number, e.FullName, e.Designation, e.Department, e.LegalEntityId, e.JoinDate, e.Status, e.BasicSalary, e.HousingAllowance, e.TransportAllowance, e.OtherAllowances));
        employees.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetEmployee(id), ct)).RequirePermission(P.EmployeesRead);
        employees.MapPost("", async (EmployeeInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateEmployee(input), ct);
            return Results.Created($"{Base}/employees/{created.Id}", created);
        }).RequirePermission(P.EmployeesManage);
        employees.MapPut("/{id:guid}", (Guid id, EmployeeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateEmployee(id, input), ct)).RequirePermission(P.EmployeesManage);
        employees.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteEmployee(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        var attendance = group.MapGroup("/attendance");
        attendance.MapGet("", (int year, int month, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new ListAttendance(year, month, legalEntityId), ct)).RequirePermission(P.AttendanceRead);
        attendance.MapPut("", (AttendanceInput input, ISender s, CancellationToken ct) => s.Send(new SaveAttendance(input), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteAttendance(id), ct); return Results.NoContent(); }).RequirePermission(P.AttendanceManage);

        var payroll = group.MapGroup("/payroll");
        payroll.MapPagedList<PayrollRun, PayrollSummaryDto>("", P.PayrollRead, r => new PayrollSummaryDto(r.Id, r.Number, r.LegalEntityId, r.Year, r.Month, r.Status, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay, r.PaidOn));
        payroll.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPayroll(id), ct)).RequirePermission(P.PayrollRead);
        payroll.MapGet("/{id:guid}/bank-file", async (Guid id, ISender s, CancellationToken ct) => Results.Text(await s.Send(new GetPayrollBankFile(id), ct), "text/csv")).RequirePermission(P.PayrollPay);
        payroll.MapPost("", async (GeneratePayrollInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new GeneratePayroll(input), ct);
            return Results.Created($"{Base}/payroll/{created.Id}", created);
        }).RequirePermission(P.PayrollManage);
        payroll.MapPost("/{id:guid}/recalculate", (Guid id, ISender s, CancellationToken ct) => s.Send(new RecalculatePayroll(id), ct)).RequirePermission(P.PayrollManage);
        payroll.MapPut("/{id:guid}/lines/{lineId:guid}", (Guid id, Guid lineId, AdjustLineBody b, ISender s, CancellationToken ct) => s.Send(new AdjustPayrollLine(id, lineId, b.Adjustment, b.Note), ct)).RequirePermission(P.PayrollManage);
        payroll.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeletePayroll(id), ct); return Results.NoContent(); }).RequirePermission(P.PayrollManage);
        payroll.MapPost("/{id:guid}/post", (Guid id, ISender s, CancellationToken ct) => s.Send(new PostPayroll(id), ct)).RequirePermission(P.PayrollPost);
        payroll.MapPost("/{id:guid}/pay", (Guid id, PayrollPayBody b, ISender s, CancellationToken ct) => s.Send(new PayPayroll(id, b.BankAccountId, b.Date), ct)).RequirePermission(P.PayrollPay);

        var reports = group.MapGroup("/reports");
        reports.MapGet("/gratuity", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetGratuityReport(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapPost("/gratuity/provision", (ProvisionBody b, ISender s, CancellationToken ct) => s.Send(new PostGratuityProvision(b.LegalEntityId, b.AsOf), ct)).RequirePermission(P.PayrollPost);
    }
}
