using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Leads per stage, open / won / lost, follow-ups due and the win rate (of one agent or all).</summary>
public sealed record GetLeadPipelineQuery(Guid? AgentPartyId = null) : IQuery<LeadPipelineDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }
