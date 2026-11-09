using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Raises a draft return from a customer against a sales order or an invoice.</summary>
public sealed record CreateSalesReturnCommand(SalesReturnInput Input) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
