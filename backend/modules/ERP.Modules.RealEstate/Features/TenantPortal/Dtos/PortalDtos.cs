using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record PortalInstalmentDto(int Sequence, DateOnly DueDate, decimal Amount, string State);
public sealed record PortalLeaseDto(Guid Id, string? Number, string Property, string Unit, DateOnly StartDate, DateOnly EndDate, decimal AnnualRent, string CurrencyCode, LeaseStatus Status, int PaymentFrequency, decimal SecurityDeposit, IReadOnlyList<PortalInstalmentDto> Schedule);

public sealed record PortalRequestDto(Guid Id, string? Number, Guid? LeaseId, string Issue, string? Category, Priority Priority, WorkOrderStatus Status, DateOnly ReportedOn, DateOnly? ScheduledFor, DateOnly? CompletedOn)
{
    public static PortalRequestDto From(WorkOrder w) => new(w.Id, w.Number, w.LeaseId, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ScheduledFor, w.CompletedOn);
}

public sealed record PortalRequestInput(Guid LeaseId, string Issue, string? Category = null, Priority Priority = Priority.Normal, string? Details = null);
