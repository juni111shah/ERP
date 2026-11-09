using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateAcquisitionHandler(
    IRepository<Acquisition> acquisitions, IRepository<FundingSource> sources, AcquisitionInputRules inputs, AcquisitionDtoFactory factory, ILegalEntityGuard guard, INumberingService numbers, IUnitOfWork uow, IClock clock)
    : ICommandHandler<CreateAcquisitionCommand, AcquisitionDto>
{
    public Task<AcquisitionDto> Handle(CreateAcquisitionCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The legal entity and the property must be valid.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        await inputs.RequirePropertyAsync(i, token);

        // 2. Create it numbered, with its funding sources; save.
        var acquisition = new Acquisition { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency };
        AcquisitionInputRules.Map(i, acquisition);
        acquisition.Number = await numbers.NextAsync(Docs.Acquisition, acquisition.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), token);
        acquisitions.Add(acquisition);
        sources.AddRange(i.Funding?.Select(f => AcquisitionInputRules.NewSource(f, acquisition)) ?? []);
        await acquisitions.SaveChangesAsync(token);
        return await factory.BuildAsync(acquisition, token);
    }, ct);
}
