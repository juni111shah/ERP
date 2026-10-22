using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

/// <summary>Maintenance request for a property, a unit or a common area.</summary>
[DefaultSort("-reportedOn")]
public class WorkOrder : LegalEntityScopedEntity, IHasCustomFields
{
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> Transitions = new()
    {
        [WorkOrderStatus.New] = [WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Assigned] = [WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.OnHold, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.OnHold] = [WorkOrderStatus.InProgress, WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Completed] = [WorkOrderStatus.Closed, WorkOrderStatus.InProgress],
        [WorkOrderStatus.Closed] = [],
        [WorkOrderStatus.Cancelled] = [],
    };

    [Listable] public string? Number { get; set; }
    [Filterable] public Guid PropertyId { get; set; }
    /// <summary>Null = common area.</summary>
    [Filterable] public Guid? UnitId { get; set; }
    [Filterable] public Guid? LeaseId { get; set; }
    [Searchable] public string Issue { get; set; } = default!;
    [Filterable, Searchable] public string? Category { get; set; }
    [Filterable, Sortable] public Priority Priority { get; set; } = Priority.Normal;
    [Filterable, Sortable] public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.New;
    [Filterable, Sortable] public DateOnly ReportedOn { get; set; }
    public string? ReportedBy { get; set; }
    [Filterable] public Guid? VendorPartyId { get; set; }
    public string? Technician { get; set; }
    public DateOnly? ScheduledFor { get; set; }
    public DateOnly? CompletedOn { get; private set; }
    public decimal EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? Details { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? CustomFields { get; set; }

    public bool IsOpen => Status is WorkOrderStatus.New or WorkOrderStatus.Assigned or WorkOrderStatus.InProgress or WorkOrderStatus.OnHold;

    public void MoveTo(WorkOrderStatus next, DateOnly today, string? resolutionNotes = null)
    {
        if (!Transitions[Status].Contains(next)) throw new DomainException($"A {Status} work order cannot move to {next}.");
        if (next == WorkOrderStatus.Assigned && VendorPartyId is null && string.IsNullOrWhiteSpace(Technician))
            throw new DomainException("Assign a vendor or technician first.");
        if (next == WorkOrderStatus.Completed) CompletedOn = today;
        if (next == WorkOrderStatus.InProgress && Status == WorkOrderStatus.Completed) CompletedOn = null; // reopened
        if (!string.IsNullOrWhiteSpace(resolutionNotes)) ResolutionNotes = resolutionNotes.Trim();
        Status = next;
    }
}

/// <summary>A sale or lease deal with commission split. Invoicing and payouts arrive with the Finance sprint.</summary>
[DefaultSort("-dealDate")]
public class BrokerageDeal : LegalEntityScopedEntity, IHasCustomFields
{
    [Listable] public string? Number { get; set; }
    [Filterable, Sortable] public DealType DealType { get; set; }
    [Filterable] public Guid? PropertyId { get; set; }
    [Filterable] public Guid? UnitId { get; set; }
    public string? UnitReference { get; set; }
    [Filterable] public Guid? ClientPartyId { get; set; }
    [Searchable] public string ClientName { get; set; } = default!;
    [Filterable] public Guid? AgentPartyId { get; set; }
    public string? ExternalAgentName { get; set; }
    public Guid? TeamLeadPartyId { get; set; }
    [Filterable, Sortable] public DateOnly DealDate { get; set; }
    [Filterable, Sortable] public DealStage Stage { get; private set; } = DealStage.Lead;
    [Sortable] public decimal DealValue { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public decimal CommissionRatePercent { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal AgentSharePercent { get; set; }
    public decimal TeamLeadSharePercent { get; set; }
    /// <summary>Deal value x rate, rounded to 2 dp.</summary>
    [Sortable] public decimal GrossCommission { get; set; }
    public string? LostReason { get; private set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    /// <summary>Stages the pipeline can reach without Finance (invoiced/collected/paid need the commission invoice).</summary>
    public static readonly DealStage[] PipelineStages = [DealStage.Lead, DealStage.Viewing, DealStage.Negotiation, DealStage.Agreed];

    public void ComputeCommission() => GrossCommission = Math.Round(DealValue * CommissionRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

    public void MoveTo(DealStage next, string? lostReason = null)
    {
        if (Stage == DealStage.Lost) throw new DomainException("A lost deal cannot move to another stage.");
        if (next == DealStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(lostReason)) throw new DomainException("A reason is required to mark a deal as lost.");
            if (Stage >= DealStage.Invoiced) throw new DomainException("A deal with a commission invoice cannot be lost; credit the invoice first.");
            Stage = DealStage.Lost;
            LostReason = lostReason.Trim();
            return;
        }
        if (next < Stage && Stage >= DealStage.Invoiced) throw new DomainException("A deal with a commission invoice cannot move backwards.");
        if (next >= DealStage.Invoiced) throw new DomainException("Invoiced, collected and agent-paid stages are set by the Finance module (commission invoicing arrives with it).");
        Stage = next;
    }
}
