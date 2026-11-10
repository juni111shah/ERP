using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Moves a requisition to another status by hand (submit, approve through the approval gate, reject).</summary>
public sealed record ChangeRequisitionStatusCommand(Guid Id, PurchaseRequisitionStatus Status) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
