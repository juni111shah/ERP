using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record RateInput(Guid? ProjectId, Guid? UserId, string? Role, decimal BillRate, decimal CostRate);

public sealed record RateDto(Guid Id, Guid? ProjectId, Guid? UserId, string? UserName, string? Role, decimal BillRate, decimal CostRate)
{
    public static RateDto From(ProjectRate r, string? userName) => new(r.Id, r.ProjectId, r.UserId, userName, r.Role, r.BillRate, r.CostRate);
}
