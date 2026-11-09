using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record PositionInput(string Code, string Title, string? Grade = null, bool IsActive = true);

public sealed record PositionDto(Guid Id, string Code, string Title, string? Grade, bool IsActive)
{
    public static PositionDto From(Position p) => new(p.Id, p.Code, p.Title, p.Grade, p.IsActive);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Position, PositionDto>> Projection = p => new PositionDto(p.Id, p.Code, p.Title, p.Grade, p.IsActive);
}
