using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

internal sealed class UpdateLegalEntityHandler(IRepository<LegalEntity> legalEntities, ICustomFieldService fields) : ICommandHandler<UpdateLegalEntityCommand, LegalEntityDto>
{
    public async Task<LegalEntityDto> Handle(UpdateLegalEntityCommand command, CancellationToken ct)
    {
        var entity = await legalEntities.GetForUpdateAsync(command.Id, ct);
        LegalEntityFields.Map(command.Input, entity);
        await fields.ApplyAsync(LegalEntityFields.EntityType, entity, command.Input.CustomFields, isCreate: false, ct);
        await legalEntities.SaveChangesAsync(ct);
        return LegalEntityDto.From(entity, await fields.ReadAsync(LegalEntityFields.EntityType, entity, ct));
    }
}
