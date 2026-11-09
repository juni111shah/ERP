using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Deletes a draft purchase return.</summary>
public sealed record DeletePurchaseReturnCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
