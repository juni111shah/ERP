using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a draft lease.</summary>
public sealed record UpdateLeaseCommand(Guid Id, LeaseInput Input) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}
