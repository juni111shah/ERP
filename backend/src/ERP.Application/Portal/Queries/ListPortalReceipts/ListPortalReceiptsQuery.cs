using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal customer's latest issued receipts.</summary>
public sealed record ListPortalReceiptsQuery : IQuery<IReadOnlyList<PortalReceiptDto>>;
