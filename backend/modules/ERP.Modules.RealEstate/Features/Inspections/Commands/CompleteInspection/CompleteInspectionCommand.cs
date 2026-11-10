using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Finishes the inspection; with RaiseWorkOrders a repair work order is created for every poor/damaged item.</summary>
public sealed record CompleteInspectionCommand(Guid Id, bool RaiseWorkOrders) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }
