using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Raises the order's invoice, delivering whatever was not delivered yet (or returns the invoice it already has).</summary>
public sealed record ConvertOrderToInvoiceCommand(Guid Id) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
