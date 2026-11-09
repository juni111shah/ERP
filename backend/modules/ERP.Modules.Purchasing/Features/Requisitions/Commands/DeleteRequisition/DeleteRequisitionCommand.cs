using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Deletes a requisition that is not ordered yet.</summary>
public sealed record DeleteRequisitionCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
