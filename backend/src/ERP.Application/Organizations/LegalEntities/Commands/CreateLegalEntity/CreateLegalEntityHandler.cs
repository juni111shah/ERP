using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

internal sealed class CreateLegalEntityHandler(IRepository<LegalEntity> legalEntities, INumberingService numbers, ICustomFieldService fields, ERP.Application.Platform.ISubscriptionLimits limits)
    : ICommandHandler<CreateLegalEntityCommand, LegalEntityDto>
{
    public async Task<LegalEntityDto> Handle(CreateLegalEntityCommand command, CancellationToken ct)
    {
        await limits.EnsureLegalEntitySlotAsync(ct);
        var entity = new LegalEntity { Code = await numbers.NextAsync(LegalEntityFields.EntityType, ct: ct) };
        LegalEntityFields.Map(command.Input, entity);
        await fields.ApplyAsync(LegalEntityFields.EntityType, entity, command.Input.CustomFields, isCreate: true, ct);
        legalEntities.Add(entity);
        await legalEntities.SaveChangesAsync(ct);
        return LegalEntityDto.From(entity, await fields.ReadAsync(LegalEntityFields.EntityType, entity, ct));
    }
}
