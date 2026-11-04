using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Wps;

/// <summary>Everything a salary-file builder needs: the run (with lines), the employees on it, the settings and the entity's currency.</summary>
public sealed record WpsInput(PayrollRun Run, IReadOnlyList<PayrollLine> Lines, IReadOnlyDictionary<Guid, Employee> Employees, WpsSettings Settings);

/// <summary>Salary-file settings of the employing legal entity. <c>Currency</c> is the entity's base currency; <c>NowUtc</c> stamps the file.</summary>
public sealed record WpsSettings(string Currency, DateTime NowUtc, string? EstablishmentId, string? EmployerBankRoutingCode, string? Columns, string? Header);

/// <summary>A salary-file format for one country. Builders throw <c>ConflictException</c> when the file cannot be prepared (missing data, layout not configured).</summary>
public interface IWpsFileBuilder
{
    /// <summary>ISO country code of the legal entity this format is for.</summary>
    string CountryCode { get; }
    string Build(WpsInput input);
}
