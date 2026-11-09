using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Takes back a payment recorded by mistake. The next daily run works out the client's state again.</summary>
public sealed record ReopenPlatformInvoiceCommand(Guid Id) : ICommand<InvoiceDocument>;
