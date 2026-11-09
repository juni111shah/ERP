using System.Globalization;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Printing;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

/// <summary>Builds payslips of a posted or paid run (view, PDF and email); shared by the payslip handlers.</summary>
public sealed class PayslipDocuments(
    IPayrollRunRepository runs, IEmployeeRepository employees, IReadRepository<Organization> orgs, ILegalEntityGuard guard, ICurrentUser user, IEmailSender email, ISender sender)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<EmailSentDto> SendAsync(PayrollRun run, PayslipDto p, string to, string? message, CancellationToken ct)
        => await sender.Send(new EmailGenericDocumentCommand("payslip", p.LineId.ToString(), await ToDocumentAsync(run, p, ct), to.Trim(), message), ct);

    public void EnsureEmailConfigured()
    {
        if (!email.IsEnabled) throw new ConflictException("Email is not configured for this system. Ask an administrator to set the Email settings.");
    }

    public async Task<PayrollRun> LoadRunAsync(Guid id, CancellationToken ct)
    {
        var run = await runs.GetWithLinesAsync(id, ct);
        if (run.Status is not (PayrollStatus.Posted or PayrollStatus.Paid)) throw new ConflictException("Post the payroll before issuing payslips.");
        return run;
    }

    public async Task<PayslipDto> BuildAsync(PayrollRun run, PayrollLine line, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(run.LegalEntityId, ct);
        var orgId = user.OrgId ?? Guid.Empty;
        var company = (await orgs.FindAsync(orgId, ct))?.Name ?? "";
        var emp = await employees.FindPayslipDetailsAsync(line.EmployeeId, ct);

        var earnings = new List<PayslipAmount> { new("Basic salary", line.Basic), new("Allowances", line.Allowances) };
        if (line.Overtime != 0) earnings.Add(new($"Overtime ({line.OvertimeHours.ToString("0.##", Inv)} h)", line.Overtime));
        var deductions = new List<PayslipAmount>();
        if (line.Absence != 0) deductions.Add(new($"Unpaid absence ({line.AbsentDays} day(s))", line.Absence));
        if (line.Adjustment > 0) earnings.Add(new(string.IsNullOrWhiteSpace(line.AdjustmentNote) ? "Adjustment" : $"Adjustment: {line.AdjustmentNote}", line.Adjustment));
        else if (line.Adjustment < 0) deductions.Add(new(string.IsNullOrWhiteSpace(line.AdjustmentNote) ? "Adjustment" : $"Adjustment: {line.AdjustmentNote}", -line.Adjustment));
        if (line.Deductions != 0) deductions.Add(new("Loan and advance recovery", line.Deductions));

        var earlier = await runs.ListPostedOrPaidWithLinesUpToAsync(run.LegalEntityId, run.Year, run.Month, ct);
        var mine = earlier.SelectMany(r => r.Lines).Where(l => l.EmployeeId == line.EmployeeId).ToList();

        return new PayslipDto(run.Id, line.Id, run.Number, run.Status, company, entity.Name, entity.BaseCurrency, line.EmployeeId, line.EmployeeNumber ?? "", line.EmployeeName, emp?.Designation, emp?.Department, emp?.Email,
            run.Year, run.Month, run.PaidOn, line.AbsentDays, earnings, deductions, earnings.Sum(x => x.Amount), deductions.Sum(x => x.Amount), line.NetPay, mine.Sum(l => l.GrossEarnings), mine.Sum(l => l.NetPay));
    }

    public async Task<PrintGenericDto> ToDocumentAsync(PayrollRun run, PayslipDto p, CancellationToken ct)
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
