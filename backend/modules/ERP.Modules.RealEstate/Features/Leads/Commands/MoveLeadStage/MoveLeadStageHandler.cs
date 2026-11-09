using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class MoveLeadStageHandler(IRepository<Lead> leads) : ICommandHandler<MoveLeadStageCommand, LeadDto>
{
    public async Task<LeadDto> Handle(MoveLeadStageCommand cmd, CancellationToken ct)
    {
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("A won lead cannot move.");
        if (l.Stage == LeadStage.Lost && cmd.Stage != LeadStage.New) throw new ConflictException("A lost lead can only be reopened as New.");
        l.Stage = cmd.Stage;
        l.LostReason = cmd.Stage == LeadStage.Lost ? cmd.LostReason!.Trim() : null;
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }
}
