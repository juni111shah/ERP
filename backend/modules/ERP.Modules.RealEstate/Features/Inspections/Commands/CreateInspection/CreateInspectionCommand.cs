using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Records an inspection with its checklist; the damage cost is the repair cost of poor and damaged items.</summary>
public sealed record CreateInspectionCommand(InspectionInput Input) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }
