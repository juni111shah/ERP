using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

public sealed record VendorWorkOrderDto(Guid Id, string? Number, string Issue, string? Category, Priority Priority, WorkOrderStatus Status, DateOnly ReportedOn, DateOnly? ScheduledFor, DateOnly? CompletedOn, decimal EstimatedCost, decimal? ActualCost, string? ResolutionNotes);
public sealed record VendorWorkOrderUpdate(WorkOrderStatus Status, decimal? ActualCost = null, string? Notes = null);

/// <summary>Work orders assigned to the signed-in vendor.</summary>
public sealed record ListVendorWorkOrders : IQuery<IReadOnlyList<VendorWorkOrderDto>>;
/// <summary>The vendor reports progress on their own order: start, hold or complete (with the actual cost and notes). Assignment, closing and cancelling stay with staff.</summary>
public sealed record UpdateVendorWorkOrder(Guid Id, VendorWorkOrderUpdate Update) : ICommand<VendorWorkOrderDto>;

public sealed class UpdateVendorWorkOrderValidator : AbstractValidator<UpdateVendorWorkOrder>
{
    public UpdateVendorWorkOrderValidator()
    {
        RuleFor(x => x.Update.Status).Must(s => s is WorkOrderStatus.InProgress or WorkOrderStatus.OnHold or WorkOrderStatus.Completed).WithMessage("You can set an order to in progress, on hold or completed.");
        RuleFor(x => x.Update.ActualCost).GreaterThanOrEqualTo(0).When(x => x.Update.ActualCost is not null);
        RuleFor(x => x.Update.Notes).MaximumLength(4000);
    }
}

internal sealed class VendorPortalHandlers(ICurrentUser user, IRepository<WorkOrder> workOrders, IClock clock) :
    IRequestHandler<ListVendorWorkOrders, IReadOnlyList<VendorWorkOrderDto>>,
    IRequestHandler<UpdateVendorWorkOrder, VendorWorkOrderDto>
{
    private Guid Me => user.PortalKind == "vendor" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();

    public async Task<IReadOnlyList<VendorWorkOrderDto>> Handle(ListVendorWorkOrders q, CancellationToken ct)
    {
        var me = Me;
        var rows = await workOrders.Query(new Specification<WorkOrder>(w => w.VendorPartyId == me)).OrderByDescending(w => w.ReportedOn).Take(200).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<VendorWorkOrderDto> Handle(UpdateVendorWorkOrder cmd, CancellationToken ct)
    {
        var me = Me;
        var w = await workOrders.Tracked(new Specification<WorkOrder>(x => x.Id == cmd.Id && x.VendorPartyId == me)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var u = cmd.Update;
        if (u.ActualCost is { } cost) w.ActualCost = cost;
        try { w.MoveTo(u.Status, DateOnly.FromDateTime(clock.UtcNow), u.Notes); }
        catch (ERP.Domain.Common.DomainException e) { throw new ConflictException(e.Message); }
        await workOrders.SaveChangesAsync(ct);
        return Map(w);
    }

    private static VendorWorkOrderDto Map(WorkOrder w) => new(w.Id, w.Number, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ScheduledFor, w.CompletedOn, w.EstimatedCost, w.ActualCost, w.ResolutionNotes);
}
