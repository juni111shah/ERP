using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Constants and rules shared by the work order handlers; no I/O.</summary>
public static class WorkOrderRules
{
    /// <summary>Takes the credit for labour and overhead moved into WIP. Falls back to the general expense role in a workspace that has not set one up.</summary>
    public const string AbsorptionRole = "manufacturing.absorption";
    /// <summary>Takes the cost of scrapped units. Falls back to cost of goods sold.</summary>
    public const string ScrapRole = "manufacturing.scrap";
    internal const string JournalSource = "manufacturing";

    internal static DateOnly Today => DateOnly.FromDateTime(AppClock.UtcNow);

    internal static DateOnly DateOrToday(DateOnly? date, string field)
    {
        var today = Today;
        if (date is { } d && d > today) throw new FieldValidationException(field, "The date cannot be in the future.");
        return date ?? today;
    }

    /// <summary>What is on the order now, per item: issues less returns, leaving out anything cancelled or reversed.</summary>
    internal static Dictionary<Guid, (decimal Quantity, decimal Value)> NetIssued(WorkOrder wo)
        => wo.MaterialIssues.Where(i => i.ReversedAt is null).GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => (g.Sum(i => i.Quantity), g.Sum(i => i.Value)));
}

/// <summary>A quantity of one item to issue to a work order.</summary>
internal sealed record MaterialNeed(Guid ItemId, string Code, string Name, decimal Quantity, string? Notes = null);
