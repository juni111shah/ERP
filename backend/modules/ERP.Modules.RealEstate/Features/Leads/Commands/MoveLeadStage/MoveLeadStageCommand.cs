using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Moves a lead along the pipeline. Losing needs a reason; winning is done by converting the lead to a deal.</summary>
public sealed record MoveLeadStageCommand(Guid Id, LeadStage Stage, string? LostReason = null) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
