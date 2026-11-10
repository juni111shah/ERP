using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a unit type; a changed code must stay unique.</summary>
public sealed record UpdateUnitTypeCommand(Guid Id, UnitTypeInput Input) : ICommand<UnitTypeDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitTypesManage;
}
