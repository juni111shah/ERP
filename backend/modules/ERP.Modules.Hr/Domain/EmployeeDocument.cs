using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum EmployeeDocumentType { Passport = 1, Visa = 2, ResidenceId = 3, LabourCard = 4, Medical = 5, Contract = 6, Certificate = 7, Other = 8 }

/// <summary>An identity, permit or contract document of an employee, with its number and expiry date. Files are attached through the shared attachments (owner type <c>hr.employeedocument</c>).</summary>
[DefaultSort("expiryDate")]
public class EmployeeDocument : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Filterable, Sortable] public EmployeeDocumentType Type { get; set; }
    [Searchable] public string? Number { get; set; }
    public DateOnly? IssueDate { get; set; }
    [Sortable] public DateOnly? ExpiryDate { get; set; }
    public string? IssuedBy { get; set; }
    public string? Notes { get; set; }
}
