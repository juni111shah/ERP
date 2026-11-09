using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Sets who bears the cost of a work order and the vendor bill that paid for it.</summary>
public sealed record SetWorkOrderCostBasisCommand(Guid Id, CostBasisInput Input) : ICommand<WorkOrderDto>, IRequirePermission { public string RequiredPermission => P.WorkOrdersEdit; }
