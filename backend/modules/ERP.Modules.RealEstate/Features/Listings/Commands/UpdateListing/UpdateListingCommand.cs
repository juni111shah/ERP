using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a listing that is not closed; it stays on its property.</summary>
public sealed record UpdateListingCommand(Guid Id, ListingInput Input) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
