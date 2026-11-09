using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record HolidayInput(DateOnly Date, string Name);

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name)
{
    public static HolidayDto From(PublicHoliday h) => new(h.Id, h.Date, h.Name);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PublicHoliday, HolidayDto>> Projection = h => new HolidayDto(h.Id, h.Date, h.Name);
}
