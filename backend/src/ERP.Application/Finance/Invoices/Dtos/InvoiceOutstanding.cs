namespace ERP.Application.Finance;

/// <summary>An open invoice and what is still outstanding on it.</summary>
public sealed record InvoiceOutstanding(Guid Id, decimal Outstanding);
