using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>One purchase requisition with its lines.</summary>
public sealed record GetRequisitionQuery(Guid Id) : IQuery<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsRead; }
