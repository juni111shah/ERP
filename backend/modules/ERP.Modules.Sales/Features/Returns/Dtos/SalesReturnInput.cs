namespace ERP.Modules.Sales.Features;

/// <summary>One of OrderLineId or InvoiceLineId, matching what the return is raised against.</summary>
public sealed record SalesReturnLineInput(Guid? OrderLineId, Guid? InvoiceLineId, decimal Quantity, bool Restock = true);
/// <summary>Exactly one of SalesOrderId or InvoiceId.</summary>
public sealed record SalesReturnInput(Guid? SalesOrderId, Guid? InvoiceId, DateOnly ReturnDate, string Reason, List<SalesReturnLineInput> Lines, string? Notes = null);
