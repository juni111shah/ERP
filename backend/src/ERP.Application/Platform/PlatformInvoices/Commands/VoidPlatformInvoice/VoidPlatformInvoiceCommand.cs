using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Voids an open invoice; a client left with nothing overdue goes back to active.</summary>
public sealed record VoidPlatformInvoiceCommand(Guid Id, string? Reason) : ICommand<InvoiceDocument>;
