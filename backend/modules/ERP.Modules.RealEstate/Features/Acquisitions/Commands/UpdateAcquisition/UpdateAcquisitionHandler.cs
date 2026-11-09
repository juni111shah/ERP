using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateAcquisitionHandler(
    IRepository<Acquisition> acquisitions, IFundingSourceRepository sources, AcquisitionInputRules inputs, AcquisitionDtoFactory factory, IUnitOfWork uow)
    : ICommandHandler<UpdateAcquisitionCommand, AcquisitionDto>
{
    public Task<AcquisitionDto> Handle(UpdateAcquisitionCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a draft can be edited, and it stays in its legal entity.
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, token);
        if (a.Status != AcquisitionStatus.Draft) throw new ConflictException("Financing is fixed once the purchase completes; only a draft can be edited.");
        if (cmd.Input.LegalEntityId != a.LegalEntityId) throw new FieldValidationException("legalEntityId", "An acquisition cannot move to another legal entity.");
        await inputs.RequirePropertyAsync(cmd.Input, token);

        // 2. Apply the form and replace the funding sources; save.
        AcquisitionInputRules.Map(cmd.Input, a);
        foreach (var old in await sources.ListOfAcquisitionForUpdateAsync(a.Id, token)) sources.Remove(old);
        sources.AddRange(cmd.Input.Funding?.Select(f => AcquisitionInputRules.NewSource(f, a)) ?? []);
        await acquisitions.SaveChangesAsync(token);
        return await factory.BuildAsync(a, token);
    }, ct);
}
