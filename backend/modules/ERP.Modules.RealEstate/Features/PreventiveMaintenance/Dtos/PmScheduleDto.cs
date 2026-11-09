using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record PmScheduleInput(Guid PropertyId, Guid? UnitId, string Title, int FrequencyMonths, DateOnly NextDue, Priority Priority = Priority.Normal, string? Category = null,
    int LeadDays = 7, decimal EstimatedCost = 0, bool IsActive = true, string? Notes = null);

public sealed record PmScheduleDto(Guid Id, Guid PropertyId, Guid? UnitId, string Title, string? Category, Priority Priority, int FrequencyMonths, DateOnly NextDue, int LeadDays, decimal EstimatedCost, bool IsActive, DateOnly? LastGeneratedOn, string? Notes)
{
    public static PmScheduleDto From(PmSchedule s) => new(s.Id, s.PropertyId, s.UnitId, s.Title, s.Category, s.Priority, s.FrequencyMonths, s.NextDue, s.LeadDays, s.EstimatedCost, s.IsActive, s.LastGeneratedOn, s.Notes);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PmSchedule, PmScheduleDto>> Projection = s => new PmScheduleDto(s.Id, s.PropertyId, s.UnitId, s.Title, s.Category, s.Priority, s.FrequencyMonths, s.NextDue, s.LeadDays, s.EstimatedCost, s.IsActive, s.LastGeneratedOn, s.Notes);
}

public sealed record PmRunResultDto(int WorkOrdersCreated);
