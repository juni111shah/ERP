using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One inspection with its checklist.</summary>
public sealed record GetInspectionQuery(Guid Id) : IQuery<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsRead; }
