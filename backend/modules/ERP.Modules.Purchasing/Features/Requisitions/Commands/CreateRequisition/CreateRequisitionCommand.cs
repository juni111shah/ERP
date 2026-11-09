using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Creates a draft purchase requisition.</summary>
public sealed record CreateRequisitionCommand(RequisitionInput Input) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
