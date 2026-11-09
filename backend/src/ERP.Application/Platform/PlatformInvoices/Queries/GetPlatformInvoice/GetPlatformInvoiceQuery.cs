using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>One invoice to a client, ready to print.</summary>
public sealed record GetPlatformInvoiceQuery(Guid Id) : IQuery<InvoiceDocument>;
