using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record EmployeeDocumentInput(Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy, string? Notes);
public sealed record EmployeeDocumentDto(Guid Id, Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy, string? Notes, int? DaysLeft, bool IsExpired)
{
    public static EmployeeDocumentDto From(EmployeeDocument d, DateOnly today)
    {
        var left = DocumentExpiry.DaysLeft(d.ExpiryDate, today);
        return new(d.Id, d.EmployeeId, d.Type, d.Number, d.IssueDate, d.ExpiryDate, d.IssuedBy, d.Notes, left, left < 0);
    }
}
public sealed record EmployeeDocumentRow(Guid Id, Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<EmployeeDocument, EmployeeDocumentRow>> Projection = d => new EmployeeDocumentRow(d.Id, d.EmployeeId, d.Type, d.Number, d.IssueDate, d.ExpiryDate, d.IssuedBy);
}
public sealed record ExpiringDocumentDto(Guid Id, Guid EmployeeId, string EmployeeNumber, string EmployeeName, EmployeeDocumentType Type, string? Number, DateOnly ExpiryDate, int DaysLeft, bool IsExpired);
