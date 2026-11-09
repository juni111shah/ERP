using System.Text;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetPayrollBankFileHandler(IPayrollRunRepository runs, IEmployeeRepository employees) : IQueryHandler<GetPayrollBankFileQuery, string>
{
    public async Task<string> Handle(GetPayrollBankFileQuery query, CancellationToken ct)
    {
        // 1. Only a posted (or paid) run; optionally only the unpaid lines.
        var run = await runs.GetWithLinesAsync(query.Id, ct);
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Post the payroll before preparing the bank file.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        var fileLines = query.UnpaidOnly ? run.Lines.Where(l => l.PaidOn is null && run.Status != PayrollStatus.Paid).ToList() : run.Lines;

        // 2. One CSV row per line, with the employee's IBAN.
        var ids = fileLines.Select(l => l.EmployeeId).ToList();
        var ibans = await employees.IbansByIdAsync(ids, ct);
        var sb = new StringBuilder("Employee no.,Name,IBAN,Net pay\n");
        foreach (var l in fileLines.OrderBy(l => l.EmployeeNumber))
            sb.Append(Csv(l.EmployeeNumber)).Append(',').Append(Csv(l.EmployeeName)).Append(',').Append(Csv(ibans.GetValueOrDefault(l.EmployeeId))).Append(',').Append(l.NetPay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return sb.ToString();
    }

    private static string Csv(string? v) => v is null ? "" : $"\"{v.Replace("\"", "\"\"")}\"";
}
