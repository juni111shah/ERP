using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a draft listing for a property or unit.</summary>
public sealed record CreateListingCommand(ListingInput Input) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
