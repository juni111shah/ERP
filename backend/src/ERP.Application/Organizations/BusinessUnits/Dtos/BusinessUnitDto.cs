using System.Text.Json.Nodes;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

public sealed record BusinessUnitInput(Guid LegalEntityId, string Code, string Name, string? Address, bool IsActive = true, JsonObject? CustomFields = null, string? Kind = null);

public sealed record BusinessUnitDto(Guid Id, Guid LegalEntityId, string Code, string Name, string? Address, bool IsActive, JsonObject CustomFields, string Kind = "branch")
{
    public static BusinessUnitDto From(BusinessUnit b, JsonObject customFields) => new(b.Id, b.LegalEntityId, b.Code, b.Name, b.Address, b.IsActive, customFields, b.Kind);
}
