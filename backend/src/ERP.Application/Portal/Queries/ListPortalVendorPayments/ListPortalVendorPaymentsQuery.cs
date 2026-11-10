using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal vendor's latest issued payments.</summary>
public sealed record ListPortalVendorPaymentsQuery : IQuery<IReadOnlyList<PortalPaymentDto>>;
