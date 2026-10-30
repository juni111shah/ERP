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
        payroll.MapGet("/{id:guid}/wps-sif", async (Guid id, ISender s, CancellationToken ct) => Results.Text(await s.Send(new GetPayrollWpsFile(id), ct), "text/csv")).RequirePermission(P.PayrollPay);
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

        var cycles = group.MapGroup("/review-cycles");
        cycles.MapPagedList<ReviewCycle, ReviewCycleDto>("", P.ReviewsRead, c => new ReviewCycleDto(c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status));
        cycles.MapPost("", async (ReviewCycleInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/review-cycles", await s.Send(new CreateReviewCycle(input), ct))).RequirePermission(P.ReviewsManage);
        cycles.MapPut("/{id:guid}", (Guid id, ReviewCycleInput input, ISender s, CancellationToken ct) => s.Send(new UpdateReviewCycle(id, input), ct)).RequirePermission(P.ReviewsManage);

        var reviews = group.MapGroup("/reviews");
        reviews.MapPagedList<PerformanceReview, ReviewRow>("", P.ReviewsRead, r => new ReviewRow(r.Id, r.EmployeeId, r.CycleId, r.ReviewerName, r.OverallRating, r.Status, r.SubmittedAt));
        reviews.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetReview(id), ct)).RequirePermission(P.ReviewsRead);
        reviews.MapPost("", async (ReviewInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateReview(input), ct);
            return Results.Created($"{Base}/reviews/{created.Id}", created);
        }).RequirePermission(P.ReviewsManage);
        reviews.MapPut("/{id:guid}", (Guid id, ReviewInput input, ISender s, CancellationToken ct) => s.Send(new UpdateReview(id, input), ct)).RequirePermission(P.ReviewsManage);
        reviews.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteReview(id), ct); return Results.NoContent(); }).RequirePermission(P.ReviewsManage);
        reviews.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) => s.Send(new SubmitReview(id), ct)).RequirePermission(P.ReviewsManage);
        reviews.MapPost("/{id:guid}/acknowledge", (Guid id, ISender s, CancellationToken ct) => s.Send(new AcknowledgeReview(id), ct)).RequirePermission(P.ReviewsManage);

        var leaveTypes = group.MapGroup("/leave-types");
        leaveTypes.MapPagedList<LeaveType, LeaveTypeDto>("", P.LeaveRead, t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.IsPaid, t.AnnualDays, t.CarryForwardCap, t.RequiresApproval, t.IsActive));
        leaveTypes.MapPost("", async (LeaveTypeInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/leave-types", await s.Send(new CreateLeaveType(input), ct))).RequirePermission(P.LeaveApprove);
        leaveTypes.MapPut("/{id:guid}", (Guid id, LeaveTypeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateLeaveType(id, input), ct)).RequirePermission(P.LeaveApprove);

        var holidays = group.MapGroup("/holidays");
        holidays.MapPagedList<PublicHoliday, HolidayDto>("", P.LeaveRead, h => new HolidayDto(h.Id, h.Date, h.Name));
        holidays.MapPost("", async (HolidayInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/holidays", await s.Send(new CreateHoliday(input), ct))).RequirePermission(P.LeaveApprove);
        holidays.MapPut("/{id:guid}", (Guid id, HolidayInput input, ISender s, CancellationToken ct) => s.Send(new UpdateHoliday(id, input), ct)).RequirePermission(P.LeaveApprove);
        holidays.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteHoliday(id), ct); return Results.NoContent(); }).RequirePermission(P.LeaveApprove);

        var leave = group.MapGroup("/leave");
        leave.MapPagedList<LeaveRequest, LeaveRequestRow>("", P.LeaveRead, r => new LeaveRequestRow(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason));
        leave.MapGet("/days", (DateOnly from, DateOnly to, ISender s, CancellationToken ct) => s.Send(new GetLeaveDays(from, to), ct)).RequirePermission(P.LeaveRead);
        leave.MapGet("/balances", (int? year, Guid? employeeId, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetLeaveBalances(year ?? DateTime.UtcNow.Year, employeeId, legalEntityId), ct)).RequirePermission(P.LeaveRead);
        leave.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetLeaveRequest(id), ct)).RequirePermission(P.LeaveRead);
        leave.MapPost("", async (LeaveRequestInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateLeaveRequest(input), ct);
            return Results.Created($"{Base}/leave/{created.Id}", created);
        }).RequirePermission(P.LeaveRequest);
        leave.MapPut("/{id:guid}", (Guid id, LeaveRequestInput input, ISender s, CancellationToken ct) => s.Send(new UpdateLeaveRequest(id, input), ct)).RequirePermission(P.LeaveRequest);
        leave.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) => s.Send(new SubmitLeaveRequest(id), ct)).RequirePermission(P.LeaveRequest);
        leave.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelLeaveRequest(id), ct)).RequirePermission(P.LeaveRequest);
        leave.MapPost("/{id:guid}/approve", (Guid id, LeaveDecisionInput? body, ISender s, CancellationToken ct) => s.Send(new ApproveLeaveRequest(id, body?.Comment), ct)).RequirePermission(P.LeaveApprove);
        leave.MapPost("/{id:guid}/reject", (Guid id, LeaveDecisionInput? body, ISender s, CancellationToken ct) => s.Send(new RejectLeaveRequest(id, body?.Comment), ct)).RequirePermission(P.LeaveApprove);

        var reports = group.MapGroup("/reports");
        reports.MapGet("/gratuity", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetGratuityReport(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapPost("/gratuity/provision", (ProvisionBody b, ISender s, CancellationToken ct) => s.Send(new PostGratuityProvision(b.LegalEntityId, b.AsOf), ct)).RequirePermission(P.PayrollPost);
    }
}
