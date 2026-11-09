using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies an employee document input onto a document (create and update).</summary>
internal static class EmployeeDocumentMapping
{
    public static void Map(EmployeeDocumentInput i, EmployeeDocument d)
    {
        d.Type = i.Type; d.Number = i.Number?.Trim(); d.IssueDate = i.IssueDate; d.ExpiryDate = i.ExpiryDate;
        d.IssuedBy = string.IsNullOrWhiteSpace(i.IssuedBy) ? null : i.IssuedBy.Trim(); d.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        if (string.IsNullOrWhiteSpace(d.Number)) d.Number = null;
    }
}
