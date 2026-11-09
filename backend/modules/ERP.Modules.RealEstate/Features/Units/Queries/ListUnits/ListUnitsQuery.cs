using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Units of the caller's workspace. Standard search / filter / sort / paging, plus tenant and rent from the active lease.</summary>
public sealed record ListUnitsQuery : PagedQuery, IQuery<PagedResult<UnitListItem>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}
