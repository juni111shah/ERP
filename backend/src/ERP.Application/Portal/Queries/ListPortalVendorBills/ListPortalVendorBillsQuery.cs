using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal vendor's latest bills (drafts, ones pending approval and voided ones left out).</summary>
public sealed record ListPortalVendorBillsQuery : IQuery<IReadOnlyList<PortalBillDto>>;
