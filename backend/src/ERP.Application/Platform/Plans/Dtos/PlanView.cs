namespace ERP.Application.Platform;

public sealed record PlanView(
    Guid Id, string Code, string Name, string? Description, bool IsPublic, bool IsActive, bool IsRecommended, int SortOrder, decimal MonthlyPrice, decimal AnnualPrice,
    string Currency, int TrialDays, int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string> Modules, IReadOnlyList<string> Features, int ClientCount);

public sealed record PlanInput(
    string Code, string Name, string? Description, bool IsPublic, bool IsRecommended, int SortOrder, decimal MonthlyPrice, decimal AnnualPrice, int TrialDays,
    int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string>? Modules, IReadOnlyList<string>? Features);
