using ERP.Application.Common;
using ERP.Application.Printing;
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
public sealed record PayrollPayBody(Guid BankAccountId, DateOnly Date, IReadOnlyList<Guid>? EmployeeIds = null);
public sealed record PayrollVoidBody(string? Reason);
public sealed record ProvisionBody(Guid LegalEntityId, DateOnly AsOf);
public sealed record LoanDisburseBody(Guid BankAccountId, DateOnly Date);
public sealed record BulkDailyBody(IReadOnlyList<DailyAttendanceInput> Items);
public sealed record RollUpBody(int Year, int Month, Guid? LegalEntityId);
public sealed record TaskDoneBody(bool Done);

/// <summary>Thin controllers: every endpoint sends one MediatR request.</summary>
internal static class HrEndpoints
{
    private const string Base = "/api/v1/m/hr";

    /// <summary>Departments, positions, org chart and the self-service ("my ...") endpoints.</summary>
    private static void MapPeople(RouteGroupBuilder group)
    {
        var departments = group.MapGroup("/departments");
        departments.MapPagedList<Department, DepartmentDto>("", P.EmployeesRead, d => new DepartmentDto(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId, d.BusinessUnitId, d.IsActive));
        departments.MapPost("", async (DepartmentInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/departments", await s.Send(new CreateDepartment(input), ct))).RequirePermission(P.EmployeesManage);
        departments.MapPut("/{id:guid}", (Guid id, DepartmentInput input, ISender s, CancellationToken ct) => s.Send(new UpdateDepartment(id, input), ct)).RequirePermission(P.EmployeesManage);
        departments.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteDepartment(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        var positions = group.MapGroup("/positions");
        positions.MapPagedList<Position, PositionDto>("", P.EmployeesRead, p => new PositionDto(p.Id, p.Code, p.Title, p.Grade, p.IsActive));
        positions.MapPost("", async (PositionInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/positions", await s.Send(new CreatePosition(input), ct))).RequirePermission(P.EmployeesManage);
        positions.MapPut("/{id:guid}", (Guid id, PositionInput input, ISender s, CancellationToken ct) => s.Send(new UpdatePosition(id, input), ct)).RequirePermission(P.EmployeesManage);
        positions.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeletePosition(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        group.MapGet("/org-chart", (ISender s, CancellationToken ct) => s.Send(new GetOrgChart(), ct)).RequirePermission(P.EmployeesRead);

        // Self-service: signed-in users with a linked employee need no permission; the employee comes from the link.
        var me = group.MapGroup("/me");
        me.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetMyEmployee(), ct)).RequireAuthorization();
        me.MapGet("/leave", ([AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListMyLeave(q), ct)).RequireAuthorization();
        me.MapPost("/leave", async (MyLeaveInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateMyLeave(input), ct);
            return Results.Created($"{Base}/me/leave/{created.Id}", created);
        }).RequireAuthorization();
        me.MapPost("/leave/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) => s.Send(new SubmitMyLeave(id), ct)).RequireAuthorization();
        me.MapPost("/leave/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelMyLeave(id), ct)).RequireAuthorization();
        me.MapGet("/balances", (int? year, ISender s, CancellationToken ct) => s.Send(new GetMyBalances(year), ct)).RequireAuthorization();
        me.MapGet("/payslips", (ISender s, CancellationToken ct) => s.Send(new ListMyPayslips(), ct)).RequireAuthorization();
        me.MapGet("/payslips/{lineId:guid}", (Guid lineId, ISender s, CancellationToken ct) => s.Send(new GetMyPayslip(lineId), ct)).RequireAuthorization();
        me.MapGet("/team/leave", ([AsParameters] PagedQuery q, string? status, ISender s, CancellationToken ct) => s.Send(new ListTeamLeave(q, status), ct)).RequireAuthorization();
    }

    public static void Map(RouteGroupBuilder group)
    {
        var employees = group.MapGroup("/employees");
        employees.MapGet("", ([AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListEmployees(q), ct)).RequirePermission(P.EmployeesRead);
        employees.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetEmployee(id), ct)).RequirePermission(P.EmployeesRead);
        employees.MapPost("", async (EmployeeInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateEmployee(input), ct);
            return Results.Created($"{Base}/employees/{created.Id}", created);
        }).RequirePermission(P.EmployeesManage);
        employees.MapPut("/{id:guid}", (Guid id, EmployeeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateEmployee(id, input), ct)).RequirePermission(P.EmployeesManage);
        employees.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteEmployee(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);
        employees.MapGet("/{id:guid}/settlement-preview", (Guid id, DateOnly terminationDate, DateOnly? noticeGivenOn, ISender s, CancellationToken ct) => s.Send(new GetSettlementPreview(id, terminationDate, noticeGivenOn), ct)).RequirePermission(P.PayrollRead);
        employees.MapPost("/{id:guid}/terminate", (Guid id, TerminateInput input, ISender s, CancellationToken ct) => s.Send(new TerminateEmployee(id, input), ct)).RequirePermission(P.EmployeesManage);
        employees.MapPost("/{id:guid}/final-settlement", async (Guid id, ISender s, CancellationToken ct) => { var run = await s.Send(new CreateFinalSettlement(id), ct); return Results.Created($"{Base}/payroll/{run.Id}", run); }).RequirePermission(P.PayrollManage);

        MapPeople(group);
        var documents = group.MapGroup("/documents");
        documents.MapPagedList<EmployeeDocument, EmployeeDocumentRow>("", P.EmployeesRead, d => new EmployeeDocumentRow(d.Id, d.EmployeeId, d.Type, d.Number, d.IssueDate, d.ExpiryDate, d.IssuedBy));
        documents.MapGet("/expiring", (int? days, ISender s, CancellationToken ct) => s.Send(new ListExpiringDocuments(days ?? 60), ct)).RequirePermission(P.EmployeesRead);
        documents.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetEmployeeDocument(id), ct)).RequirePermission(P.EmployeesRead);
        documents.MapPost("", async (EmployeeDocumentInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateEmployeeDocument(input), ct);
            return Results.Created($"{Base}/documents/{created.Id}", created);
        }).RequirePermission(P.EmployeesManage);
        documents.MapPut("/{id:guid}", (Guid id, EmployeeDocumentInput input, ISender s, CancellationToken ct) => s.Send(new UpdateEmployeeDocument(id, input), ct)).RequirePermission(P.EmployeesManage);
        documents.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteEmployeeDocument(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        var attendance = group.MapGroup("/attendance");
        attendance.MapGet("", (int year, int month, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new ListAttendance(year, month, legalEntityId), ct)).RequirePermission(P.AttendanceRead);
        attendance.MapPut("", (AttendanceInput input, ISender s, CancellationToken ct) => s.Send(new SaveAttendance(input), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteAttendance(id), ct); return Results.NoContent(); }).RequirePermission(P.AttendanceManage);

        var payroll = group.MapGroup("/payroll");
        payroll.MapPagedList<PayrollRun, PayrollSummaryDto>("", P.PayrollRead, r => new PayrollSummaryDto(r.Id, r.Number, r.LegalEntityId, r.Year, r.Month, r.Status, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay, r.PaidOn, r.Kind));
        payroll.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPayroll(id), ct)).RequirePermission(P.PayrollRead);
        payroll.MapGet("/{id:guid}/bank-file", async (Guid id, bool? unpaidOnly, ISender s, CancellationToken ct) => Results.Text(await s.Send(new GetPayrollBankFile(id, unpaidOnly == true), ct), "text/csv")).RequirePermission(P.PayrollPay);
        payroll.MapGet("/{id:guid}/wps-sif", async (Guid id, bool? unpaidOnly, ISender s, CancellationToken ct) => Results.Text(await s.Send(new GetPayrollWpsFile(id, unpaidOnly == true), ct), "text/csv")).RequirePermission(P.PayrollPay);
        payroll.MapGet("/{id:guid}/payslips/{lineId:guid}", (Guid id, Guid lineId, ISender s, CancellationToken ct) => s.Send(new GetPayslip(id, lineId), ct)).RequirePermission(P.PayrollRead);
        payroll.MapGet("/{id:guid}/payslips/{lineId:guid}/payslip.pdf", async (Guid id, Guid lineId, ISender s, CancellationToken ct) => { var f = await s.Send(new GetPayslipPdf(id, lineId), ct); return Results.File(f.Content, PrintPdfFile.ContentType, f.FileName); }).RequirePermission(P.PayrollRead);
        payroll.MapPost("/{id:guid}/payslips/{lineId:guid}/email", (Guid id, Guid lineId, PayslipEmailInput? body, ISender s, CancellationToken ct) => s.Send(new EmailPayslip(id, lineId, body ?? new(null, null)), ct)).RequirePermission(P.PayrollRead);
        payroll.MapPost("/{id:guid}/email-payslips", (Guid id, ISender s, CancellationToken ct) => s.Send(new EmailPayslips(id), ct)).RequirePermission(P.PayrollRead);
        payroll.MapPost("", async (GeneratePayrollInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new GeneratePayroll(input), ct);
            return Results.Created($"{Base}/payroll/{created.Id}", created);
        }).RequirePermission(P.PayrollManage);
        payroll.MapPost("/{id:guid}/recalculate", (Guid id, ISender s, CancellationToken ct) => s.Send(new RecalculatePayroll(id), ct)).RequirePermission(P.PayrollManage);
        payroll.MapPut("/{id:guid}/lines/{lineId:guid}", (Guid id, Guid lineId, AdjustLineBody b, ISender s, CancellationToken ct) => s.Send(new AdjustPayrollLine(id, lineId, b.Adjustment, b.Note), ct)).RequirePermission(P.PayrollManage);
        payroll.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeletePayroll(id), ct); return Results.NoContent(); }).RequirePermission(P.PayrollManage);
        payroll.MapPost("/{id:guid}/post", (Guid id, ISender s, CancellationToken ct) => s.Send(new PostPayroll(id), ct)).RequirePermission(P.PayrollPost);
        payroll.MapPost("/{id:guid}/pay", (Guid id, PayrollPayBody b, ISender s, CancellationToken ct) => s.Send(new PayPayroll(id, b.BankAccountId, b.Date, b.EmployeeIds), ct)).RequirePermission(P.PayrollPay);
        payroll.MapPost("/{id:guid}/void", (Guid id, PayrollVoidBody b, ISender s, CancellationToken ct) => s.Send(new VoidPayroll(id, b.Reason), ct)).RequirePermission(P.PayrollPost);

        var loans = group.MapGroup("/loans");
        loans.MapPagedList<EmployeeLoan, LoanRow>("", P.PayrollRead, l => new LoanRow(l.Id, l.EmployeeId, l.Kind, l.Principal, l.Remaining, l.StartYear, l.StartMonth, l.Instalments, l.Status, l.DisbursedOn));
        loans.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetLoan(id), ct)).RequirePermission(P.PayrollRead);
        loans.MapPost("", async (LoanInput input, ISender s, CancellationToken ct) => { var l = await s.Send(new CreateLoan(input), ct); return Results.Created($"{Base}/loans/{l.Id}", l); }).RequirePermission(P.PayrollManage);
        loans.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelLoan(id), ct)).RequirePermission(P.PayrollManage);
        loans.MapPost("/{id:guid}/disburse", (Guid id, LoanDisburseBody b, ISender s, CancellationToken ct) => s.Send(new DisburseLoan(id, b.BankAccountId, b.Date), ct)).RequirePermission(P.PayrollPay);

        var statutory = group.MapGroup("/statutory-rules");
        statutory.MapPagedList<StatutoryRule, StatutoryRuleRow>("", P.PayrollRead, r => new StatutoryRuleRow(r.Id, r.LegalEntityId, r.Code, r.Name, r.Kind, r.Method, r.Base, r.Rate, r.IsActive));
        statutory.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetStatutoryRule(id), ct)).RequirePermission(P.PayrollRead);
        statutory.MapPost("", async (StatutoryRuleInput input, ISender s, CancellationToken ct) => { var r = await s.Send(new CreateStatutoryRule(input), ct); return Results.Created($"{Base}/statutory-rules/{r.Id}", r); }).RequirePermission(P.PayrollManage);
        statutory.MapPut("/{id:guid}", (Guid id, StatutoryRuleInput input, ISender s, CancellationToken ct) => s.Send(new UpdateStatutoryRule(id, input), ct)).RequirePermission(P.PayrollManage);
        statutory.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteStatutoryRule(id), ct); return Results.NoContent(); }).RequirePermission(P.PayrollManage);
        payroll.MapGet("/{id:guid}/statutory", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPayrollStatutory(id), ct)).RequirePermission(P.PayrollRead);

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
        leaveTypes.MapPagedList<LeaveType, LeaveTypeDto>("", P.LeaveRead, t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.IsPaid, t.AnnualDays, t.CarryForwardCap, t.RequiresApproval, t.IsActive, t.ProRataFirstYear, t.AccrualMode, t.RequiresAttachmentAfterDays,
            t.Tiers.OrderBy(x => (double)x.FromServiceYears).Select(x => new LeaveTierDto(x.FromServiceYears, x.Days)).ToList(), t.Encashable, t.PayBands.OrderBy(x => x.FromDay).Select(x => new LeavePayBandDto(x.FromDay, x.PayPercent)).ToList()));
        leaveTypes.MapPost("", async (LeaveTypeInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/leave-types", await s.Send(new CreateLeaveType(input), ct))).RequirePermission(P.LeaveApprove);
        leaveTypes.MapPut("/{id:guid}", (Guid id, LeaveTypeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateLeaveType(id, input), ct)).RequirePermission(P.LeaveApprove);

        var holidays = group.MapGroup("/holidays");
        holidays.MapPagedList<PublicHoliday, HolidayDto>("", P.LeaveRead, h => new HolidayDto(h.Id, h.Date, h.Name));
        holidays.MapPost("", async (HolidayInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/holidays", await s.Send(new CreateHoliday(input), ct))).RequirePermission(P.LeaveApprove);
        holidays.MapPut("/{id:guid}", (Guid id, HolidayInput input, ISender s, CancellationToken ct) => s.Send(new UpdateHoliday(id, input), ct)).RequirePermission(P.LeaveApprove);
        holidays.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteHoliday(id), ct); return Results.NoContent(); }).RequirePermission(P.LeaveApprove);

        var leave = group.MapGroup("/leave");
        leave.MapPagedList<LeaveRequest, LeaveRequestRow>("", P.LeaveRead, r => new LeaveRequestRow(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason));
        leave.MapPost("/apply-country-defaults", (int? year, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new ApplyLeaveCountryDefaults(year ?? DateTime.UtcNow.Year, legalEntityId), ct)).RequirePermission(P.LeaveApprove);
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
        leave.MapPost("/{id:guid}/approve", (Guid id, LeaveDecisionInput? body, ISender s, CancellationToken ct) => s.Send(new ApproveLeaveRequest(id, body?.Comment), ct)).RequireAuthorization(); // approver permission or line manager: checked in the handler
        leave.MapPost("/{id:guid}/reject", (Guid id, LeaveDecisionInput? body, ISender s, CancellationToken ct) => s.Send(new RejectLeaveRequest(id, body?.Comment), ct)).RequireAuthorization();

        var reports = group.MapGroup("/reports");
        reports.MapGet("/summary", (DateOnly? from, DateOnly? to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetHrSummaryReport(from, to, legalEntityId), ct)).RequirePermission(P.EmployeesRead);
        reports.MapGet("/birthdays-anniversaries", (int? days, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetBirthdaysAnniversaries(days ?? 30, legalEntityId), ct)).RequirePermission(P.EmployeesRead);
        reports.MapGet("/gratuity", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetGratuityReport(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapPost("/gratuity/provision", (ProvisionBody b, ISender s, CancellationToken ct) => s.Send(new PostGratuityProvision(b.LegalEntityId, b.AsOf), ct)).RequirePermission(P.PayrollPost);

        // shifts
        var shifts = group.MapGroup("/shifts");
        shifts.MapGet("", (ISender s, CancellationToken ct) => s.Send(new ListShifts(), ct)).RequirePermission(P.AttendanceRead);
        shifts.MapPost("", async (ShiftInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/shifts", await s.Send(new CreateShift(input), ct))).RequirePermission(P.AttendanceManage);
        shifts.MapPut("/{id:guid}", (Guid id, ShiftInput input, ISender s, CancellationToken ct) => s.Send(new UpdateShift(id, input), ct)).RequirePermission(P.AttendanceManage);
        shifts.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteShift(id), ct); return Results.NoContent(); }).RequirePermission(P.AttendanceManage);
        shifts.MapPost("/{id:guid}/assign", async (Guid id, AssignShiftInput b, ISender s, CancellationToken ct) => new { assigned = await s.Send(new AssignShift(id, b.EmployeeIds), ct) }).RequirePermission(P.AttendanceManage);
        shifts.MapPost("/unassign", async (AssignShiftInput b, ISender s, CancellationToken ct) => new { cleared = await s.Send(new UnassignShift(b.EmployeeIds), ct) }).RequirePermission(P.AttendanceManage);

        // daily attendance
        attendance.MapGet("/daily", (int year, int month, Guid? employeeId, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new ListDailyAttendance(year, month, employeeId, legalEntityId), ct)).RequirePermission(P.AttendanceRead);
        attendance.MapPost("/punch", (PunchesBody b, ISender s, CancellationToken ct) => s.Send(new RecordPunches(b.Punches, b.Source), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapPut("/daily", (DailyAttendanceInput input, ISender s, CancellationToken ct) => s.Send(new SaveDailyAttendance(input), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapPost("/daily/bulk", (BulkDailyBody body, ISender s, CancellationToken ct) => s.Send(new BulkSaveDailyAttendance(body.Items), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapPost("/daily/roll-up", (RollUpBody body, ISender s, CancellationToken ct) => s.Send(new RollUpAttendance(body.Year, body.Month, body.LegalEntityId), ct)).RequirePermission(P.AttendanceManage);
        attendance.MapDelete("/daily/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteDailyAttendance(id), ct); return Results.NoContent(); }).RequirePermission(P.AttendanceManage);

        // history, onboarding tasks
        employees.MapGet("/{id:guid}/history", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetEmployeeHistory(id), ct)).RequirePermission(P.EmployeesRead);
        employees.MapPost("/{id:guid}/history", (Guid id, HistoryNoteInput input, ISender s, CancellationToken ct) => s.Send(new AddEmployeeNote(id, input), ct)).RequirePermission(P.EmployeesManage);
        employees.MapGet("/{id:guid}/tasks", (Guid id, ISender s, CancellationToken ct) => s.Send(new ListEmployeeTasks(id), ct)).RequirePermission(P.EmployeesRead);
        employees.MapPost("/{id:guid}/tasks", (Guid id, EmployeeTaskInput input, ISender s, CancellationToken ct) => s.Send(new AddEmployeeTask(id, input), ct)).RequirePermission(P.EmployeesManage);
        employees.MapPost("/{id:guid}/checklists/{templateId:guid}", (Guid id, Guid templateId, ISender s, CancellationToken ct) => s.Send(new ApplyChecklist(id, templateId), ct)).RequirePermission(P.EmployeesManage);
        var tasks = group.MapGroup("/tasks");
        tasks.MapGet("/open", (ISender s, CancellationToken ct) => s.Send(new ListOpenTasks(), ct)).RequirePermission(P.EmployeesRead);
        tasks.MapPost("/{id:guid}/done", (Guid id, TaskDoneBody body, ISender s, CancellationToken ct) => s.Send(new SetEmployeeTaskDone(id, body.Done), ct)).RequirePermission(P.EmployeesManage);
        tasks.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteEmployeeTask(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        var checklists = group.MapGroup("/checklists");
        checklists.MapPagedList<ChecklistTemplate, ChecklistTemplateRow>("", P.EmployeesRead, t => new ChecklistTemplateRow(t.Id, t.Name, t.Kind, t.IsActive));
        checklists.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetChecklistTemplate(id), ct)).RequirePermission(P.EmployeesRead);
        checklists.MapPost("", async (ChecklistTemplateInput input, ISender s, CancellationToken ct) => { var t = await s.Send(new CreateChecklistTemplate(input), ct); return Results.Created($"{Base}/checklists/{t.Id}", t); }).RequirePermission(P.EmployeesManage);
        checklists.MapPut("/{id:guid}", (Guid id, ChecklistTemplateInput input, ISender s, CancellationToken ct) => s.Send(new UpdateChecklistTemplate(id, input), ct)).RequirePermission(P.EmployeesManage);
        checklists.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteChecklistTemplate(id), ct); return Results.NoContent(); }).RequirePermission(P.EmployeesManage);

        // recruitment
        var openings = group.MapGroup("/openings");
        openings.MapPagedList<JobOpening, JobOpeningRow>("", P.RecruitmentRead, o => new JobOpeningRow(o.Id, o.Title, o.DepartmentId, o.Headcount, o.Status, o.ClosingDate));
        openings.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetJobOpening(id), ct)).RequirePermission(P.RecruitmentRead);
        openings.MapPost("", async (JobOpeningInput input, ISender s, CancellationToken ct) => { var o = await s.Send(new CreateJobOpening(input), ct); return Results.Created($"{Base}/openings/{o.Id}", o); }).RequirePermission(P.RecruitmentManage);
        openings.MapPut("/{id:guid}", (Guid id, JobOpeningInput input, ISender s, CancellationToken ct) => s.Send(new UpdateJobOpening(id, input), ct)).RequirePermission(P.RecruitmentManage);
        openings.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteJobOpening(id), ct); return Results.NoContent(); }).RequirePermission(P.RecruitmentManage);
        var candidates = group.MapGroup("/candidates");
        candidates.MapPagedList<Candidate, CandidateRow>("", P.RecruitmentRead, c => new CandidateRow(c.Id, c.OpeningId, c.FullName, c.Email, c.Stage, c.StageChangedAt));
        candidates.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCandidate(id), ct)).RequirePermission(P.RecruitmentRead);
        candidates.MapPost("", async (CandidateInput input, ISender s, CancellationToken ct) => { var c = await s.Send(new CreateCandidate(input), ct); return Results.Created($"{Base}/candidates/{c.Id}", c); }).RequirePermission(P.RecruitmentManage);
        candidates.MapPut("/{id:guid}", (Guid id, CandidateInput input, ISender s, CancellationToken ct) => s.Send(new UpdateCandidate(id, input), ct)).RequirePermission(P.RecruitmentManage);
        candidates.MapPost("/{id:guid}/move", (Guid id, MoveCandidateInput input, ISender s, CancellationToken ct) => s.Send(new MoveCandidate(id, input), ct)).RequirePermission(P.RecruitmentManage);
        candidates.MapPost("/{id:guid}/hire", async (Guid id, HireInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/employees", await s.Send(new HireCandidate(id, input), ct))).RequirePermission(P.RecruitmentManage);
    }
}
