using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal vendor's open bills as of a date and what is still owed to them.</summary>
public sealed record GetPortalVendorStatementQuery(DateOnly AsOf) : IQuery<PortalPayablesDto>;
