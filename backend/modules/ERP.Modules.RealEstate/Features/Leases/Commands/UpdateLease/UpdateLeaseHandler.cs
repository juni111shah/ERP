using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateLeaseHandler(IRepository<Lease> leases, LeaseInputRules inputs, LeaseDtoFactory factory, ICustomFieldService fields)
    : ICommandHandler<UpdateLeaseCommand, LeaseDto>
{
    public async Task<LeaseDto> Handle(UpdateLeaseCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft can be edited.
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        if (lease.Status != LeaseStatus.Draft)
            throw new ConflictException("Only a draft lease can be edited. Use an amendment, renewal or termination for an active lease.");

        // 2. A new unit must be free to let.
        var (unit, _, _, _) = await inputs.LoadContextAsync(cmd.Input, ct);
        if (unit.Id != lease.UnitId && unit.Status is not (UnitStatus.Vacant or UnitStatus.Reserved))
            throw new FieldValidationException("unitId", $"Unit {unit.UnitNumber} is {unit.Status}; only vacant or reserved units can be let.");

        // 3. Apply the form and the custom fields, save.
        LeaseInputRules.Map(cmd.Input, lease, unit);
        await fields.ApplyAsync(Docs.Lease, lease, cmd.Input.CustomFields, isCreate: false, ct);
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }
}
