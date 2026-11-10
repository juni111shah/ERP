namespace ERP.Modules.Hr.Features;

public sealed record GratuityRow(Guid EmployeeId, string Number, string Employee, Guid LegalEntityId, DateOnly Joined, decimal BasicSalary, decimal ServiceYears, bool Eligible, decimal EntitlementDays, decimal Gratuity);
public sealed record GratuityReportDto(DateOnly AsOf, IReadOnlyList<GratuityRow> Rows, decimal TotalLiability, string? Rule);
public sealed record GratuityProvisionDto(DateOnly AsOf, decimal Liability, decimal Provision, decimal Posted, Guid? JournalId);
