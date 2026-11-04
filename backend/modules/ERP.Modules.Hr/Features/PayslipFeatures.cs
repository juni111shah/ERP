using System.Globalization;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record PayslipAmount(string Label, decimal Amount);
public sealed record PayslipDto(
    Guid RunId, Guid LineId, string RunNumber, PayrollStatus RunStatus, string Company, string LegalEntityName, string Currency,
    Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? Designation, string? Department, string? Email, int Year, int Month, DateOnly? PaidOn, int AbsentDays,
    IReadOnlyList<PayslipAmount> Earnings, IReadOnlyList<PayslipAmount> Deductions, decimal TotalEarnings, decimal TotalDeductions, decimal NetPay, decimal YtdGross, decimal YtdNet);
public sealed record PayslipEmailInput(string? To, string? Message);
public sealed record PayslipBulkEmailResult(int Sent, int Skipped, IReadOnlyList<string> SkippedEmployees);

public sealed record GetPayslip(Guid RunId, Guid LineId) : IQuery<PayslipDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
public sealed record GetPayslipPdf(Guid RunId, Guid LineId) : IQuery<PrintPdfFile>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
public sealed record EmailPayslip(Guid RunId, Guid LineId, PayslipEmailInput Input) : ICommand<EmailSentDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
/// <summary>Emails every payslip of a posted or paid run to its employee's email address; employees without one are skipped.</summary>
public sealed record EmailPayslips(Guid RunId) : ICommand<PayslipBulkEmailResult>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }

internal sealed class PayslipHandlers(
    IReadRepository<PayrollRun> runs, IReadRepository<Employee> employees, IReadRepository<Organization> orgs,
    ILegalEntityGuard guard, ICurrentUser user, IEmailSender email, IPrintPdfRenderer renderer, ISender sender) :
    IRequestHandler<GetPayslip, PayslipDto>,
    IRequestHandler<GetPayslipPdf, PrintPdfFile>,
    IRequestHandler<EmailPayslip, EmailSentDto>,
    IRequestHandler<EmailPayslips, PayslipBulkEmailResult>
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<PayslipDto> Handle(GetPayslip q, CancellationToken ct)
    {
        var run = await LoadRunAsync(q.RunId, ct);
        return await BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == q.LineId) ?? throw new NotFoundException(), ct);
    }

    public async Task<PrintPdfFile> Handle(GetPayslipPdf q, CancellationToken ct)
    {
        var run = await LoadRunAsync(q.RunId, ct);
        var p = await BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == q.LineId) ?? throw new NotFoundException(), ct);
        return new(renderer.Render(await ToDocumentAsync(run, p, ct)), PrintPdfNames.FileName("Payslip", $"{p.EmployeeNumber}-{p.Year}{p.Month:00}", p.LineId));
    }

    public async Task<EmailSentDto> Handle(EmailPayslip cmd, CancellationToken ct)
    {
        EnsureEmailConfigured();
        var run = await LoadRunAsync(cmd.RunId, ct);
        var p = await BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == cmd.LineId) ?? throw new NotFoundException(), ct);
        var to = string.IsNullOrWhiteSpace(cmd.Input.To) ? p.Email : cmd.Input.To;
        if (string.IsNullOrWhiteSpace(to)) throw new FieldValidationException("to", "This employee has no email address. Enter one to send to.");
        return await SendAsync(run, p, to, cmd.Input.Message, ct);
    }

    public async Task<PayslipBulkEmailResult> Handle(EmailPayslips cmd, CancellationToken ct)
    {
        EnsureEmailConfigured();
        var run = await LoadRunAsync(cmd.RunId, ct);
        int sent = 0; var skipped = new List<string>();
        foreach (var line in run.Lines.OrderBy(l => l.EmployeeNumber))
        {
            var p = await BuildAsync(run, line, ct);
            if (string.IsNullOrWhiteSpace(p.Email)) { skipped.Add($"{p.EmployeeNumber} {p.EmployeeName}: no email address"); continue; }
            try { await SendAsync(run, p, p.Email, null, ct); sent++; }
            catch (Exception e) when (e is ConflictException or FieldValidationException) { skipped.Add($"{p.EmployeeNumber} {p.EmployeeName}: {e.Message}"); }
        }
        return new PayslipBulkEmailResult(sent, skipped.Count, skipped);
    }

    private async Task<EmailSentDto> SendAsync(PayrollRun run, PayslipDto p, string to, string? message, CancellationToken ct)
        => await sender.Send(new EmailGenericDocument("payslip", p.LineId.ToString(), await ToDocumentAsync(run, p, ct), to.Trim(), message), ct);

    private void EnsureEmailConfigured()
    {
        if (!email.IsEnabled) throw new ConflictException("Email is not configured for this system. Ask an administrator to set the Email settings.");
    }

    private async Task<PayrollRun> LoadRunAsync(Guid id, CancellationToken ct)
    {
        var run = await runs.Query(new Specification<PayrollRun>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (run.Status is not (PayrollStatus.Posted or PayrollStatus.Paid)) throw new ConflictException("Post the payroll before issuing payslips.");
        return run;
    }

    private async Task<PayslipDto> BuildAsync(PayrollRun run, PayrollLine line, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(run.LegalEntityId, ct);
        var orgId = user.OrgId ?? Guid.Empty;
        var company = await orgs.Query(new Specification<Organization>(o => o.Id == orgId)).Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";
        var emp = await employees.Query(new Specification<Employee>(e => e.Id == line.EmployeeId)).Select(e => new { e.Designation, e.Department, e.Email }).FirstOrDefaultAsync(ct);

        var earnings = new List<PayslipAmount> { new("Basic salary", line.Basic), new("Allowances", line.Allowances) };
        if (line.Overtime != 0) earnings.Add(new($"Overtime ({line.OvertimeHours.ToString("0.##", Inv)} h)", line.Overtime));
        var deductions = new List<PayslipAmount>();
        if (line.Absence != 0) deductions.Add(new($"Unpaid absence ({line.AbsentDays} day(s))", line.Absence));
        if (line.Adjustment > 0) earnings.Add(new(string.IsNullOrWhiteSpace(line.AdjustmentNote) ? "Adjustment" : $"Adjustment: {line.AdjustmentNote}", line.Adjustment));
        else if (line.Adjustment < 0) deductions.Add(new(string.IsNullOrWhiteSpace(line.AdjustmentNote) ? "Adjustment" : $"Adjustment: {line.AdjustmentNote}", -line.Adjustment));
        if (line.Deductions != 0) deductions.Add(new("Loan and advance recovery", line.Deductions));

        var earlier = await runs.Query(new Specification<PayrollRun>(r => r.LegalEntityId == run.LegalEntityId && r.Year == run.Year && r.Month <= run.Month && (r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid)).Include(r => r.Lines)).ToListAsync(ct);
        var mine = earlier.SelectMany(r => r.Lines).Where(l => l.EmployeeId == line.EmployeeId).ToList();

        return new PayslipDto(run.Id, line.Id, run.Number, run.Status, company, entity.Name, entity.BaseCurrency, line.EmployeeId, line.EmployeeNumber ?? "", line.EmployeeName, emp?.Designation, emp?.Department, emp?.Email,
            run.Year, run.Month, run.PaidOn, line.AbsentDays, earnings, deductions, earnings.Sum(x => x.Amount), deductions.Sum(x => x.Amount), line.NetPay, mine.Sum(l => l.GrossEarnings), mine.Sum(l => l.NetPay));
    }

    private async Task<PrintGenericDto> ToDocumentAsync(PayrollRun run, PayslipDto p, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(run.LegalEntityId, ct);
        var header = new PrintHeaderDto(p.Company, entity.Name, entity.Address, entity.TaxRegistrationNo, entity.RegistrationNo, entity.Phone, entity.Email, entity.BaseCurrency, entity.CountryCode);
        var rows = p.Earnings.Select(x => new[] { x.Label, Money(x.Amount) })
            .Concat(p.Deductions.Select(x => new[] { "Less: " + x.Label, "-" + Money(x.Amount) })).ToList();
        var party = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Designation)) party.Add(p.Designation!);
        if (!string.IsNullOrWhiteSpace(p.Department)) party.Add(p.Department!);
        party.Add("Employee no.: " + p.EmployeeNumber);
        var meta = new List<(string, string)> { ("Pay period", new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy", Inv)), ("Payroll run", p.RunNumber), ("Currency", p.Currency) };
        if (p.PaidOn is { } paid) meta.Add(("Paid on", paid.ToString("dd MMM yyyy", Inv)));
        return new PrintGenericDto(header, "Payslip", $"{p.EmployeeNumber} {p.Year}-{p.Month:00}", p.RunStatus == PayrollStatus.Paid ? "Paid" : null, "Employee", p.EmployeeName, party, meta,
            [new("Description", 6f), new($"Amount ({p.Currency})", 2f, true)], rows,
            [new("Total earnings", Money(p.TotalEarnings)), new("Total deductions", Money(p.TotalDeductions)), new($"Net pay ({p.Currency})", Money(p.NetPay), true),
             new("Year to date gross", Money(p.YtdGross)), new("Year to date net", Money(p.YtdNet))], null);
    }

    private static string Money(decimal v) => v.ToString("N2", Inv);
}
