using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The portal customer's open items as of a date, with unapplied receipts and the balance.</summary>
public sealed record GetPortalStatementQuery(DateOnly AsOf) : IQuery<PortalStatementDto>;
