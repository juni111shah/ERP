using ERP.Domain.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record InvoiceRow(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, string Description, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn, bool IsOverdue)
{
    internal static InvoiceRow From(SubscriptionInvoice i, DateOnly today)
        => new(i.Id, i.Number, i.Status, i.Description, i.Total, i.Currency, i.IssuedOn, i.DueOn, i.PaidOn, i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today);
}

/// <summary>An invoice with everything needed to print it.</summary>
public sealed record InvoiceDocument(
    Guid Id, string Number, SubscriptionInvoiceStatus Status, Guid OrgId, string ClientName, string ClientCode, string? BillingEmail, string Description, DateOnly? PeriodStart, DateOnly? PeriodEnd,
    decimal Subtotal, decimal TaxPercent, string TaxLabel, decimal TaxAmount, decimal Total, string Currency, DateOnly IssuedOn, DateOnly DueOn, DateOnly? PaidOn,
    string? PaymentMethod, string? PaymentReference, string? VoidReason, string CompanyName, string? CompanyAddress, string? CompanyTaxNumber, string? SupportEmail, string? PaymentInstructions)
{
    internal static InvoiceDocument From(SubscriptionInvoice i, Organization client, WorkspaceSubscription? s, PlatformSetting settings) => new(
        i.Id, i.Number, i.Status, i.OrgId, client.Name, client.Code, s?.BillingEmail, i.Description, i.PeriodStart, i.PeriodEnd, i.Subtotal, i.TaxPercent, string.IsNullOrEmpty(i.TaxLabel) ? settings.TaxLabel : i.TaxLabel, i.TaxAmount, i.Total, i.Currency,
        i.IssuedOn, i.DueOn, i.PaidOn, i.PaymentMethod, i.PaymentReference, i.VoidReason, SubscriptionInvoicing.CompanyOf(settings), settings.CompanyAddress, settings.CompanyTaxNumber, settings.SupportEmail, settings.PaymentInstructions);
}
