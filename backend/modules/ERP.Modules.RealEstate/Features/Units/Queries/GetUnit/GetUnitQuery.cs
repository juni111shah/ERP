using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One unit with its custom fields.</summary>
public sealed record GetUnitQuery(Guid Id) : IQuery<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}
