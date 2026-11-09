using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Replaces a requisition that is not ordered yet; any approval is released and the requisition goes back to draft.</summary>
public sealed record UpdateRequisitionCommand(Guid Id, RequisitionInput Input) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
