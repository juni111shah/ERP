using ERP.Domain.Common;

namespace ERP.Modules.Distribution.Domain;

public enum TripStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum WholesaleTier
{
    Standard = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3,
    Platinum = 4
}

public class DistributionRoute : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public Guid? AssignedDriverUserId { get; set; }
    public string? DriverName { get; set; }
    public string? VehicleNumber { get; set; }
    public decimal? VehicleCapacityKg { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }

    public ICollection<DeliveryStop> Stops { get; set; } = new List<DeliveryStop>();
}

public class DeliveryStop : LegalEntityScopedEntity
{
    public Guid RouteId { get; set; }
    public Guid CustomerPartyId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public string? PreferredTimeWindow { get; set; }
    public string? SpecialInstructions { get; set; }
    public bool IsActive { get; set; } = true;

    public DistributionRoute Route { get; set; } = null!;
}

public class VanSalesTrip : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string TripNumber { get; set; } = string.Empty;

    public Guid RouteId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public Guid DriverUserId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public DateOnly DepartureDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Scheduled;
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalCollectionsAmount { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    public ICollection<TripInventoryItem> InventoryItems { get; set; } = new List<TripInventoryItem>();
}

public class TripInventoryItem : LegalEntityScopedEntity
{
    public Guid TripId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Uom { get; set; } = "PCS";
    public decimal LoadedQuantity { get; set; }
    public decimal SoldQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
    public decimal UnitPrice { get; set; }

    public VanSalesTrip Trip { get; set; } = null!;
}

public class WholesalePriceMatrix : LegalEntityScopedEntity
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public WholesaleTier Tier { get; set; } = WholesaleTier.Standard;
    public decimal MinQuantity { get; set; } = 1;
    public decimal? MaxQuantity { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal WholesaleUnitPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
