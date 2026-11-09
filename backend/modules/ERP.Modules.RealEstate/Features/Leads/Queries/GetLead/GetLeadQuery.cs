using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One lead.</summary>
public sealed record GetLeadQuery(Guid Id) : IQuery<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }
