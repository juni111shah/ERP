using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class MoveDealStageHandler(IRepository<BrokerageDeal> deals, ICustomFieldService fields) : ICommandHandler<MoveDealStageCommand, DealDto>
{
    public async Task<DealDto> Handle(MoveDealStageCommand cmd, CancellationToken ct)
    {
        var deal = await deals.GetForUpdateAsync(cmd.Id, ct);
        deal.MoveTo(cmd.Stage, cmd.LostReason);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }
}
