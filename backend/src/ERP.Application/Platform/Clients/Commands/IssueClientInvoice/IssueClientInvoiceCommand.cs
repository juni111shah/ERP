using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Issues an invoice to a client: a one-off charge when an amount is given, otherwise the next period of the plan.</summary>
public sealed record IssueClientInvoiceCommand(Guid OrgId, IssueInvoiceInput Input) : ICommand<InvoiceRow>;
