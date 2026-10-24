using ERP.Domain.Common;

namespace ERP.Modules.Manufacturing.Domain;

public enum WorkOrderStatus
{
    Draft = 0,
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum OperationStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}

public class BillOfMaterials : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    public Guid ProducedItemId { get; set; }
    public string ProducedItemCode { get; set; } = string.Empty;
    public string ProducedItemName { get; set; } = string.Empty;
    public decimal OutputQuantity { get; set; } = 1;
    public string Uom { get; set; } = "PCS";
    public string Version { get; set; } = "1.0";
    public decimal LaborCostPerUnit { get; set; }
    public decimal OverheadCostPerUnit { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? CustomFields { get; set; }

    public ICollection<BomComponent> Components { get; set; } = new List<BomComponent>();
}

public class BomComponent : LegalEntityScopedEntity
{
    public Guid BomId { get; set; }
    public Guid ComponentItemId { get; set; }
    public string ComponentItemCode { get; set; } = string.Empty;
    public string ComponentItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public string Uom { get; set; } = "PCS";
    public decimal ScrapPercentage { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }

    public BillOfMaterials Bom { get; set; } = null!;
}

public class WorkOrder : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string WorkOrderNumber { get; set; } = string.Empty;

    public Guid BomId { get; set; }
    public string BomCode { get; set; } = string.Empty;
    public Guid ProducedItemId { get; set; }
    public string ProducedItemCode { get; set; } = string.Empty;
    public string ProducedItemName { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public decimal CompletedQuantity { get; set; }
    public decimal ScrappedQuantity { get; set; }
    public string Uom { get; set; } = "PCS";
    public Guid WarehouseId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;
    public decimal EstimatedTotalCost { get; set; }
    public decimal ActualTotalCost { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    public ICollection<WorkOrderOperation> Operations { get; set; } = new List<WorkOrderOperation>();
}

public class WorkOrderOperation : LegalEntityScopedEntity
{
    public Guid WorkOrderId { get; set; }
    public int SequenceOrder { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string WorkCenterName { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal HourlyRate { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Pending;

    public WorkOrder WorkOrder { get; set; } = null!;
}

public class ProductCostingRecord : LegalEntityScopedEntity
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal StandardTotalUnitCost { get; set; }
    public decimal LatestActualUnitCost { get; set; }
    public decimal CostVariance { get; set; }
    public DateOnly CalculationDate { get; set; }
}
