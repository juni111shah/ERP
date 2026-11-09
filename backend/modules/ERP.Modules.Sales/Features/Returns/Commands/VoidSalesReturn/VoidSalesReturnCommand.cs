using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Reverses the stock receipt and its journal and voids the credit note. Refused once the credit note is applied or the stock is used.</summary>
public sealed record VoidSalesReturnCommand(Guid Id, string? Reason) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
