using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Deletes a draft sales return.</summary>
public sealed record DeleteSalesReturnCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
