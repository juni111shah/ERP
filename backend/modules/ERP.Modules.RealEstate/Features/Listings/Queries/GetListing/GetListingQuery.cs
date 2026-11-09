using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One listing.</summary>
public sealed record GetListingQuery(Guid Id) : IQuery<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }
