using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateDealHandler(IRepository<BrokerageDeal> deals, DealInputRules inputs, ICustomFieldService fields) : ICommandHandler<UpdateDealCommand, DealDto>
{
    public async Task<DealDto> Handle(UpdateDealCommand cmd, CancellationToken ct)
    {
        // 1. Only a deal before invoicing (and not lost) can be edited, and it stays in its legal entity.
        var deal = await deals.GetForUpdateAsync(cmd.Id, ct);
        if (deal.Stage is DealStage.Lost or >= DealStage.Invoiced) throw new ConflictException($"A deal in stage {deal.Stage} can no longer be edited.");
        if (cmd.Input.LegalEntityId != deal.LegalEntityId) throw new FieldValidationException("legalEntityId", "A deal cannot move to another legal entity.");

        // 2. Check and apply the form and the custom fields, save.
        await inputs.ValidateReferencesAsync(cmd.Input, ct);
        DealInputRules.Map(cmd.Input, deal);
        await fields.ApplyAsync(Docs.Deal, deal, cmd.Input.CustomFields, isCreate: false, ct);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }
}
