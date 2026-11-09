using System.Text.Json.Nodes;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record ProjectInput(
    Guid LegalEntityId,
    string? Number,
    string Name,
    string? Description,
    Guid? CustomerId,
    string? CustomerName,
    string? ManagerName,
    ProjectStatus Status,
    ProjectBillingType BillingType,
    decimal BudgetAmount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    JsonObject? CustomFields = null,
    Guid? ManagerUserId = null,
    Guid? TaxCodeId = null,
    decimal? BudgetHours = null,
    string? SourceType = null,
    Guid? SourceId = null,
    bool? IsTemplate = null,
    decimal? ContractValue = null);

public sealed record CloneInput(string Name, DateOnly? StartDate, Guid? CustomerId, bool IncludeTeam = false, bool AsTemplate = false);
