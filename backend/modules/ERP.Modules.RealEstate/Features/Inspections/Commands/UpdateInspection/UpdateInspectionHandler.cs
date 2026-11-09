using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateInspectionHandler(IInspectionRepository inspections, IRepository<InspectionItem> itemRows, InspectionInputRules inputs, InspectionDtoFactory factory)
    : ICommandHandler<UpdateInspectionCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(UpdateInspectionCommand cmd, CancellationToken ct)
    {
        // 1. Only an open inspection can be edited, and it stays on its property.
        var i = await inspections.GetWithItemsForUpdateAsync(cmd.Id, ct);
        if (i.Status == InspectionStatus.Completed) throw new ConflictException("A completed inspection can no longer be edited.");
        if (cmd.Input.PropertyId != i.PropertyId) throw new FieldValidationException("propertyId", "An inspection cannot move to another property.");

        // 2. Apply the form and replace the checklist.
        await inputs.MapAsync(cmd.Input, i, ct);
        foreach (var old in i.Items.ToList()) itemRows.Remove(old);
        var fresh = InspectionInputRules.Items(i, cmd.Input);
        itemRows.AddRange(fresh);
        i.DamageCost = fresh.Where(x => x.Condition >= ItemCondition.Poor).Sum(x => x.RepairCost);

        // 3. Save.
        await inspections.SaveChangesAsync(ct);
        i.Items.Clear();
        i.Items.AddRange(fresh);
        return await factory.BuildAsync(i, ct);
    }
}
