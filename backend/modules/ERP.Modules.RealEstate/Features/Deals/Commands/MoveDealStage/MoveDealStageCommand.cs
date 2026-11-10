using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Moves a deal through the pipeline (Lead -> Viewing -> Negotiation -> Agreed) or marks it lost (needs a reason).</summary>
public sealed record MoveDealStageCommand(Guid Id, DealStage Stage, string? LostReason = null) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => Stage == DealStage.Lost ? P.DealsLose : P.DealsEdit;
}
