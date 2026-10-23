using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record PortalInstalmentDto(int Sequence, DateOnly DueDate, decimal Amount, string State);
public sealed record PortalLeaseDto(Guid Id, string? Number, string Property, string Unit, DateOnly StartDate, DateOnly EndDate, decimal AnnualRent, string CurrencyCode, LeaseStatus Status, int PaymentFrequency, decimal SecurityDeposit, IReadOnlyList<PortalInstalmentDto> Schedule);
public sealed record PortalRequestDto(Guid Id, string? Number, Guid? LeaseId, string Issue, string? Category, Priority Priority, WorkOrderStatus Status, DateOnly ReportedOn, DateOnly? ScheduledFor, DateOnly? CompletedOn);
public sealed record PortalRequestInput(Guid LeaseId, string Issue, string? Category = null, Priority Priority = Priority.Normal, string? Details = null);

/// <summary>The signed-in tenant's leases with the rent schedule (state: scheduled, invoiced, paid, overdue).</summary>
public sealed record GetPortalLeases : IQuery<IReadOnlyList<PortalLeaseDto>>;
public sealed record ListPortalRequests : IQuery<IReadOnlyList<PortalRequestDto>>;
/// <summary>A maintenance request raised by the tenant; it becomes a work order for staff.</summary>
public sealed record CreatePortalRequest(PortalRequestInput Input) : ICommand<PortalRequestDto>;

public sealed class CreatePortalRequestValidator : AbstractValidator<CreatePortalRequest>
{
    public CreatePortalRequestValidator()
    {
        RuleFor(x => x.Input.LeaseId).NotEmpty();
        RuleFor(x => x.Input.Issue).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Input.Priority).IsInEnum();
        RuleFor(x => x.Input.Category).MaximumLength(50);
    }
}

internal sealed class PortalHandlers(ICurrentUser user, IReadRepository<Lease> leases, IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<RentInstalment> instalments,
    IReadRepository<Invoice> invoices, IRepository<WorkOrder> workOrders, IReadRepository<ERP.Domain.Parties.Party> parties, INumberingService numbers, IClock clock) :
    IRequestHandler<GetPortalLeases, IReadOnlyList<PortalLeaseDto>>,
    IRequestHandler<ListPortalRequests, IReadOnlyList<PortalRequestDto>>,
    IRequestHandler<CreatePortalRequest, PortalRequestDto>
{
    private Guid Me => user.PortalPartyId ?? throw new ForbiddenException();
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<IReadOnlyList<PortalLeaseDto>> Handle(GetPortalLeases q, CancellationToken ct)
    {
        var me = Me;
        var mine = await leases.Query(new Specification<Lease>(l => l.LesseePartyId == me && l.Status != LeaseStatus.Draft && l.Status != LeaseStatus.Cancelled)).OrderByDescending(l => l.StartDate).ToListAsync(ct);
        var ids = mine.Select(l => l.Id).ToList();
        var schedule = await instalments.Query(new Specification<RentInstalment>(i => ids.Contains(i.LeaseId) && i.Status != InstalmentStatus.Cancelled)).OrderBy(i => i.DueDate).ToListAsync(ct);
        var invoiceIds = schedule.Where(i => i.InvoiceId != null).Select(i => i.InvoiceId!.Value).Distinct().ToList();
        var invoiceMap = await invoices.Query(new Specification<Invoice>(v => invoiceIds.Contains(v.Id))).Select(v => new { v.Id, v.Total, v.Paid }).ToDictionaryAsync(v => v.Id, ct);
        var propertyIds = mine.Select(l => l.PropertyId).Distinct().ToList();
        var unitIds = mine.Select(l => l.UnitId).Distinct().ToList();
        var propNames = await properties.Query(new Specification<Property>(p => propertyIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var unitNames = await units.Query(new Specification<Unit>(u => unitIds.Contains(u.Id))).Select(u => new { u.Id, u.UnitNumber }).ToDictionaryAsync(u => u.Id, u => u.UnitNumber, ct);
        string State(RentInstalment i)
        {
            if (i.InvoiceId is { } id && invoiceMap.TryGetValue(id, out var inv)) return inv.Total - inv.Paid <= 0 ? "paid" : i.DueDate < Today ? "overdue" : "invoiced";
            return "scheduled";
        }
        return mine.Select(l => new PortalLeaseDto(l.Id, l.Number, propNames.GetValueOrDefault(l.PropertyId, ""), unitNames.GetValueOrDefault(l.UnitId, ""), l.StartDate, l.EndDate, l.AnnualRent, l.CurrencyCode, l.Status, l.PaymentFrequency, l.SecurityDepositAmount,
            schedule.Where(i => i.LeaseId == l.Id).Select(i => new PortalInstalmentDto(i.Sequence, i.DueDate, i.Amount, State(i))).ToList())).ToList();
    }

    public async Task<IReadOnlyList<PortalRequestDto>> Handle(ListPortalRequests q, CancellationToken ct)
    {
        var me = Me;
        var leaseIds = await leases.Query(new Specification<Lease>(l => l.LesseePartyId == me)).Select(l => l.Id).ToListAsync(ct);
        var rows = await workOrders.Query(new Specification<WorkOrder>(w => w.LeaseId != null && leaseIds.Contains(w.LeaseId.Value))).OrderByDescending(w => w.ReportedOn).Take(100).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<PortalRequestDto> Handle(CreatePortalRequest cmd, CancellationToken ct)
    {
        var me = Me;
        var i = cmd.Input;
        var lease = await leases.Query(new Specification<Lease>(l => l.Id == i.LeaseId && l.LesseePartyId == me)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (lease.Status is not (LeaseStatus.Active or LeaseStatus.HoldingOver)) throw new ConflictException("Requests can only be raised for an active lease.");
        var name = (await parties.GetAsync(me, ct)).Name;
        var w = new WorkOrder
        {
            LegalEntityId = lease.LegalEntityId, PropertyId = lease.PropertyId, UnitId = lease.UnitId, LeaseId = lease.Id, Issue = i.Issue.Trim(), Category = i.Category, Priority = i.Priority,
            ReportedOn = Today, ReportedBy = $"{name} (tenant portal)", Details = i.Details,
        };
        w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, Today, ct);
        workOrders.Add(w);
        await workOrders.SaveChangesAsync(ct);
        return Map(w);
    }

    private static PortalRequestDto Map(WorkOrder w) => new(w.Id, w.Number, w.LeaseId, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ScheduledFor, w.CompletedOn);
}
