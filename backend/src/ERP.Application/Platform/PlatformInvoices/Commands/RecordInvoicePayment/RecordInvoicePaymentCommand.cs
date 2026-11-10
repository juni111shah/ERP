using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Marks an open invoice paid; a paid renewal moves the client's period on and clears an overdue state.</summary>
public sealed record RecordInvoicePaymentCommand(Guid Id, RecordPaymentInput Input) : ICommand<InvoiceDocument>;
