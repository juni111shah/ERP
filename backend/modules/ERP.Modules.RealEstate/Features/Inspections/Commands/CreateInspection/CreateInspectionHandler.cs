using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateInspectionHandler(IRepository<Inspection> inspections, InspectionInputRules inputs, InspectionDtoFactory factory) : ICommandHandler<CreateInspectionCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(CreateInspectionCommand cmd, CancellationToken ct)
    {
        var i = new Inspection();
        await inputs.MapAsync(cmd.Input, i, ct);
        i.Items.AddRange(InspectionInputRules.Items(i, cmd.Input));
        i.DamageCost = Damage(i);
        inspections.Add(i);
        await inspections.SaveChangesAsync(ct);
        return await factory.BuildAsync(i, ct);
    }

    private static decimal Damage(Inspection i) => i.Items.Where(x => x.Condition >= ItemCondition.Poor).Sum(x => x.RepairCost);
}
