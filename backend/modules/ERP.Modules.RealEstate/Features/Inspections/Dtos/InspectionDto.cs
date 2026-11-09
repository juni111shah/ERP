using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record InspectionItemInput(string Area, ItemCondition Condition, string? Notes = null, decimal RepairCost = 0);
public sealed record InspectionInput(Guid PropertyId, Guid? UnitId, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, string? Notes, IReadOnlyList<InspectionItemInput> Items);
public sealed record InspectionItemDto(Guid Id, string Area, ItemCondition Condition, string? Notes, decimal RepairCost, Guid? WorkOrderId);
public sealed record InspectionDto(Guid Id, Guid PropertyId, string PropertyName, Guid? UnitId, string? UnitNumber, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, InspectionStatus Status, string? Notes, decimal DamageCost, IReadOnlyList<InspectionItemDto> Items);

public sealed record InspectionRow(Guid Id, Guid PropertyId, Guid? UnitId, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, InspectionStatus Status, decimal DamageCost)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Inspection, InspectionRow>> Projection = i => new InspectionRow(i.Id, i.PropertyId, i.UnitId, i.LeaseId, i.Kind, i.InspectionDate, i.Inspector, i.Status, i.DamageCost);
}
