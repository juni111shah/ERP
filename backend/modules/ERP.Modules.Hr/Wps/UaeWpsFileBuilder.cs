using System.Globalization;
using System.Text;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Wps;

/// <summary>UAE Wages Protection System SIF text: one EDR record per employee, then the SCR summary record. Reports everything that is missing.</summary>
public sealed class UaeWpsFileBuilder : IWpsFileBuilder
{
    public string CountryCode => "AE";

    public string Build(WpsInput input)
    {
        var run = input.Run;
        var s = input.Settings;
        var eid = s.EstablishmentId?.Trim();
        var employerBank = s.EmployerBankRoutingCode?.Trim();
        var missing = new List<string>();
        if (string.IsNullOrEmpty(eid)) missing.Add("the employer establishment ID (setting hr.employerEstablishmentId)");
        if (string.IsNullOrEmpty(employerBank)) missing.Add("the employer bank routing code (setting hr.employerBankRoutingCode)");
        var invariant = CultureInfo.InvariantCulture;
        var start = new DateOnly(run.Year, run.Month, 1);
        var end = new DateOnly(run.Year, run.Month, DateTime.DaysInMonth(run.Year, run.Month));
        var sb = new StringBuilder();
        decimal total = 0;
        foreach (var l in input.Lines.OrderBy(l => l.EmployeeNumber))
        {
            var e = input.Employees.GetValueOrDefault(l.EmployeeId);
            var who = $"{l.EmployeeNumber} {l.EmployeeName}";
            var card = Clean(e?.LabourCardNumber) ?? Clean(CustomValue(e, "cf_ae_mohre_personal_no"));
            var routing = Clean(e?.BankRoutingCode) ?? Clean(CustomValue(e, "cf_ae_bank_routing_code"));
            var iban = Clean(e?.Iban);
            if (card is null) missing.Add($"{who}: labour card / MOHRE personal number");
            if (routing is null) missing.Add($"{who}: bank routing code");
            if (iban is null) missing.Add($"{who}: IBAN");
            var variable = Math.Max(0m, PayrollMath.R(l.Overtime + l.Adjustment));
            var fixedIncome = PayrollMath.R(l.NetPay) - variable;
            total += l.NetPay;
            sb.Append("EDR,").Append(card).Append(',').Append(routing).Append(',').Append(iban).Append(',')
              .Append(start.ToString("yyyy-MM-dd")).Append(',').Append(end.ToString("yyyy-MM-dd")).Append(',').Append(end.Day).Append(',')
              .Append(fixedIncome.ToString("0.00", invariant)).Append(',').Append(variable.ToString("0.00", invariant)).Append(',').Append(l.AbsentDays).Append('\n');
        }
        if (missing.Count > 0) throw new ConflictException("The WPS file cannot be prepared yet. Missing: " + string.Join("; ", missing) + ".");
        var now = s.NowUtc;
        sb.Append("SCR,").Append(eid).Append(',').Append(employerBank).Append(',').Append(now.ToString("yyyy-MM-dd")).Append(',').Append(now.ToString("HHmm")).Append(',')
          .Append(run.Month.ToString("00")).Append(run.Year).Append(',').Append(input.Lines.Count).Append(',').Append(PayrollMath.R(total).ToString("0.00", invariant)).Append(',').Append(s.Currency).Append('\n');
        return sb.ToString();
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Replace(" ", "");

    private static string? CustomValue(Employee? e, string key)
    {
        if (e?.CustomFields is not { Length: > 0 } json) return null;
        try { return System.Text.Json.Nodes.JsonNode.Parse(json)?[key]?.GetValue<string>(); } catch { return null; }
    }
}
