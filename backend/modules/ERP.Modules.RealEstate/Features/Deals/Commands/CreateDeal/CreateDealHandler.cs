using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateDealHandler(
    IRepository<BrokerageDeal> deals, DealInputRules inputs, INumberingService numbers, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard, IClock clock)
    : ICommandHandler<CreateDealCommand, DealDto>
{
    public async Task<DealDto> Handle(CreateDealCommand cmd, CancellationToken ct)
    {
        // 1. The legal entity and everything the deal refers to must be valid.
        var i = cmd.Input;
        var entity = await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        await inputs.ValidateReferencesAsync(i, ct);

        // 2. Create it, numbered, with its custom fields; save.
        var deal = new BrokerageDeal { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency };
        DealInputRules.Map(i, deal);
        deal.Number = await numbers.NextAsync(Docs.Deal, deal.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Deal, deal, i.CustomFields, isCreate: true, ct);
        deals.Add(deal);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }
}
