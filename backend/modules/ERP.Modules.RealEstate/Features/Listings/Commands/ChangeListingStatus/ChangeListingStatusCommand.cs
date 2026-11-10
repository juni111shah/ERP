using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Publishes a draft, closes a published listing (let, sold or withdrawn), or returns a closed one to draft.</summary>
public sealed record ChangeListingStatusCommand(Guid Id, ListingStatus Status) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
