using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Templates;
using ERP.Domain.Organizations;

namespace ERP.Application.Finance;

internal sealed class GetTaxReturnHandler(TaxLineReader reader, IReadRepository<LegalEntity> entities, CountryPackRegistry packs) : IQueryHandler<GetTaxReturnQuery, TaxReturnDto>
{
    public async Task<TaxReturnDto> Handle(GetTaxReturnQuery q, CancellationToken ct)
    {
        // 1. The legal entity's country pack decides the layout.
        var entityId = q.LegalEntityId ?? throw new FieldValidationException("legalEntityId", "Choose a legal entity.");
        var entity = await entities.FindAsync(entityId, ct) ?? throw new NotFoundException("Legal entity not found.");
        var pack = packs.Find(entity.CountryCode);
        var def = pack?.TaxReturn ?? throw new ConflictException($"No tax return layout is installed for {entity.CountryCode}. Use the tax summary report instead.");

        // 2. The period's tax per code, laid out by the pack.
        var lines = await reader.ReadAsync(q.From, q.To, entityId, ct);
        var (rows, net) = TaxReturnCalculator.Compute(def, lines);
        return new(entity.CountryCode, def.Code, def.Title, def.TitleAr, q.From, q.To, rows, net, entity.BaseCurrency, def.Notes);
    }
}
