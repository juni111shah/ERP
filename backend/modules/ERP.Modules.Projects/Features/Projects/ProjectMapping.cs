using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Copies a project form onto the entity; no I/O. Shared by create and update.</summary>
public static class ProjectMapping
{
    public static void Map(ProjectInput i, Project p, string? customerName, string? managerName)
    {
        p.Name = i.Name.Trim();
        p.Description = i.Description?.Trim();
        p.CustomerId = i.CustomerId;
        p.CustomerName = i.CustomerId is null ? i.CustomerName?.Trim() : customerName;
        // What a form does not send stays as it was: a screen or an older app that only knows some fields must not wipe the others.
        var typedManager = i.ManagerName?.Trim();
        p.ManagerUserId = i.ManagerUserId ?? (string.Equals(typedManager, p.ManagerName, StringComparison.Ordinal) ? p.ManagerUserId : null);
        p.ManagerName = managerName ?? typedManager;
        p.TaxCodeId = i.TaxCodeId == Guid.Empty ? null : i.TaxCodeId ?? p.TaxCodeId;   // an empty id clears it
        p.BudgetHours = i.BudgetHours ?? p.BudgetHours;
        if (!string.IsNullOrWhiteSpace(i.SourceType)) p.SourceType = i.SourceType.Trim();
        p.SourceId = i.SourceId ?? p.SourceId;
        p.IsTemplate = i.IsTemplate ?? p.IsTemplate;
        p.Status = i.Status;
        p.BillingType = i.BillingType;
        p.BudgetAmount = i.BudgetAmount;
        p.ContractValue = i.ContractValue ?? p.ContractValue;
        p.StartDate = i.StartDate;
        p.EndDate = i.EndDate;
    }
}
