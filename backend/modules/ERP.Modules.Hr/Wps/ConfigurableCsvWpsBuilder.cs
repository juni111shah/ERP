using System.Globalization;
using System.Text;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Wps;

/// <summary>
/// Generic salary file used when no country format exists: one CSV row per employee with the columns listed in setting <c>hr.wps.columns</c>
/// (for example <c>employeeNumber,name,iban,bankRoutingCode,labourCard,netPay</c>), preceded by the text of <c>hr.wps.header</c> when it is set.
/// </summary>
public sealed class ConfigurableCsvWpsBuilder
{
    public static readonly IReadOnlyList<string> Fields = ["employeeNumber", "name", "iban", "bankRoutingCode", "labourCard", "basic", "allowances", "overtime", "absence", "adjustment", "grossPay", "deductions", "netPay", "absentDays", "currency", "month", "year"];

    public static bool IsConfigured(WpsSettings s) => !string.IsNullOrWhiteSpace(s.Columns);

    public string Build(WpsInput input)
    {
        var columns = (input.Settings.Columns ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var unknown = columns.Where(c => !Fields.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (columns.Count == 0 || unknown.Count > 0)
            throw new ConflictException(columns.Count == 0
                ? "Setting hr.wps.columns is empty."
                : $"Setting hr.wps.columns has unknown fields: {string.Join(", ", unknown)}. Allowed: {string.Join(", ", Fields)}.");
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.Settings.Header)) sb.Append(input.Settings.Header.Trim()).Append('\n');
        foreach (var l in input.Lines.OrderBy(l => l.EmployeeNumber))
        {
            var e = input.Employees.GetValueOrDefault(l.EmployeeId);
            sb.Append(string.Join(',', columns.Select(c => Csv(Value(c.ToLowerInvariant(), input, l, e))))).Append('\n');
        }
        return sb.ToString();
    }

    private static string Value(string field, WpsInput i, PayrollLine l, Employee? e)
    {
        static string M(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        return field switch
        {
            "employeenumber" => l.EmployeeNumber ?? "",
            "name" => l.EmployeeName,
            "iban" => e?.Iban?.Trim().Replace(" ", "") ?? "",
            "bankroutingcode" => e?.BankRoutingCode?.Trim() ?? "",
            "labourcard" => e?.LabourCardNumber?.Trim().Replace(" ", "") ?? "",
            "basic" => M(l.Basic),
            "allowances" => M(l.Allowances),
            "overtime" => M(l.Overtime),
            "absence" => M(l.Absence),
            "adjustment" => M(l.Adjustment),
            "grosspay" => M(l.GrossEarnings),
            "deductions" => M(l.Deductions),
            "netpay" => M(l.NetPay),
            "absentdays" => l.AbsentDays.ToString(CultureInfo.InvariantCulture),
            "currency" => i.Settings.Currency,
            "month" => i.Run.Month.ToString("00"),
            "year" => i.Run.Year.ToString(CultureInfo.InvariantCulture),
            _ => "",
        };
    }

    private static string Csv(string v) => v.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
