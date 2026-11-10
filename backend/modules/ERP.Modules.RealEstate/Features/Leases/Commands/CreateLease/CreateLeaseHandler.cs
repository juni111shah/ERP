using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateLeaseHandler(
    IRepository<Lease> leases, LeaseInputRules inputs, LeaseActivation activation, LeaseDtoFactory factory, INumberingService numbers, ICustomFieldService fields, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<CreateLeaseCommand, LeaseDto>
{
    /// <summary>One transaction: a lease created with ActivateNow is never left behind as a draft when activation fails.</summary>
    public Task<LeaseDto> Handle(CreateLeaseCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(token => CreateCoreAsync(cmd, token), ct);

    private async Task<LeaseDto> CreateCoreAsync(CreateLeaseCommand cmd, CancellationToken ct)
    {
        // 1. What the lease refers to; only a vacant or reserved unit can be let.
        var i = cmd.Input;
        var (unit, property, _, legalEntity) = await inputs.LoadContextAsync(i, ct);
        if (unit.Status is not (UnitStatus.Vacant or UnitStatus.Reserved))
            throw new FieldValidationException("unitId", $"Unit {unit.UnitNumber} is {unit.Status}; only vacant or reserved units can be let.");

        // 2. Create the draft, numbered, with its custom fields.
        var lease = new Lease { LegalEntityId = property.LegalEntityId, CurrencyCode = legalEntity.BaseCurrency };
        LeaseInputRules.Map(i, lease, unit);
        lease.Number = await numbers.NextAsync(Docs.Lease, lease.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Lease, lease, i.CustomFields, isCreate: true, ct);
        leases.Add(lease);
        await leases.SaveChangesAsync(ct);

        // 3. Activate it straight away when asked.
        return i.ActivateNow ? await activation.ActivateAsync(lease.Id, ct) : await factory.BuildAsync(lease, ct);
    }
}
