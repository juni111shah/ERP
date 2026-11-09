using System.Linq.Expressions;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

public sealed record PartyGroupInput(string Code, string Name, bool IsActive = true);

public sealed record PartyGroupDto(Guid Id, string Code, string Name, bool IsActive)
{
    public static PartyGroupDto From(PartyGroup g) => new(g.Id, g.Code, g.Name, g.IsActive);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PartyGroup, PartyGroupDto>> Projection = g => new PartyGroupDto(g.Id, g.Code, g.Name, g.IsActive);
}
