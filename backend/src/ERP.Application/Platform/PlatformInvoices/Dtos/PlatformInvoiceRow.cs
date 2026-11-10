using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record PlatformInvoiceRow(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, Guid OrgId, string ClientName, string Description, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn, string? PaymentMethod, string? PaymentReference, bool IsOverdue);
