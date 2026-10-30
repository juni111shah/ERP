using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record WorkOrderInput(
    Guid PropertyId, Guid? UnitId, string Issue, Priority Priority, Guid? LeaseId = null, string? Category = null, DateOnly? ReportedOn = null,
    string? ReportedBy = null, Guid? VendorPartyId = null, string? Technician = null, DateOnly? ScheduledFor = null, decimal EstimatedCost = 0,
    string? Details = null, JsonObject? CustomFields = null);

public sealed record WorkOrderDto(
    Guid Id, string? Number, Guid PropertyId, Guid? UnitId, Guid? LeaseId, Guid LegalEntityId, string Issue, string? Category, Priority Priority,
    WorkOrderStatus Status, DateOnly ReportedOn, string? ReportedBy, Guid? VendorPartyId, string? Technician, DateOnly? ScheduledFor, DateOnly? CompletedOn,
    decimal EstimatedCost, decimal? ActualCost, string? Details, string? ResolutionNotes, JsonObject CustomFields,
    CostResponsibility? ChargeableTo = null, decimal OwnerSharePercent = 100, Guid? BillId = null)
{
    public static WorkOrderDto From(WorkOrder w, JsonObject customFields) => new(
        w.Id, w.Number, w.PropertyId, w.UnitId, w.LeaseId, w.LegalEntityId, w.Issue, w.Category, w.Priority, w.Status, w.ReportedOn, w.ReportedBy,
        w.VendorPartyId, w.Technician, w.ScheduledFor, w.CompletedOn, w.EstimatedCost, w.ActualCost, w.Details, w.ResolutionNotes, customFields, w.ChargeableTo, w.OwnerSharePercent, w.BillId);
}

/// <summary>Who bears the cost of a work order and which vendor bill paid for it. Allowed in any status (the bill usually arrives after the work is done).</summary>
public sealed record CostBasisInput(CostResponsibility? ChargeableTo, decimal? OwnerSharePercent, Guid? BillId);
public sealed record SetWorkOrderCostBasis(Guid Id, CostBasisInput Input) : ICommand<WorkOrderDto>, IRequirePermission { public string RequiredPermission => P.WorkOrdersEdit; }

public sealed class SetWorkOrderCostBasisValidator : AbstractValidator<SetWorkOrderCostBasis>
{
    public SetWorkOrderCostBasisValidator()
    {
        RuleFor(x => x.Input.ChargeableTo).IsInEnum().When(x => x.Input.ChargeableTo is not null);
        RuleFor(x => x.Input.OwnerSharePercent).InclusiveBetween(0m, 100m).When(x => x.Input.OwnerSharePercent is not null);
        RuleFor(x => x.Input.OwnerSharePercent).NotNull().When(x => x.Input.ChargeableTo == CostResponsibility.Shared).WithMessage("Enter the owner's share for a shared cost.");
    }
}

internal sealed class WorkOrderCostBasisHandler(IRepository<WorkOrder> workOrders, IReadRepository<ERP.Domain.Finance.Bill> bills, ICustomFieldService fields) : IRequestHandler<SetWorkOrderCostBasis, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(SetWorkOrderCostBasis cmd, CancellationToken ct)
    {
        var w = await workOrders.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.BillId is { } billId)
        {
            var bill = await bills.Query(new Specification<ERP.Domain.Finance.Bill>(b => b.Id == billId)).Select(b => new { b.LegalEntityId, b.Status }).FirstOrDefaultAsync(ct)
                ?? throw new FieldValidationException("billId", "Bill not found.");
            if (bill.Status is ERP.Domain.Finance.DocumentStatus.Draft or ERP.Domain.Finance.DocumentStatus.PendingApproval or ERP.Domain.Finance.DocumentStatus.Voided)
                throw new FieldValidationException("billId", "Link a posted bill (drafts and voided bills carry no cost).");
            if (bill.LegalEntityId != w.LegalEntityId) throw new FieldValidationException("billId", "The bill belongs to another legal entity.");
        }
        w.ChargeableTo = i.ChargeableTo;
        w.OwnerSharePercent = i.ChargeableTo == CostResponsibility.Shared ? i.OwnerSharePercent!.Value : i.ChargeableTo is CostResponsibility.Company or CostResponsibility.Tenant ? 0m : 100m;
        w.BillId = i.BillId;
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(w, await fields.ReadAsync(Docs.WorkOrder, w, ct));
    }
}

public sealed record GetWorkOrder(Guid Id) : IQuery<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersRead;
}

public sealed record CreateWorkOrder(WorkOrderInput Input) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersCreate;
}

/// <summary>Edits an open work order (vendor, technician, schedule, costs...).</summary>
public sealed record UpdateWorkOrder(Guid Id, WorkOrderInput Input) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersEdit;
}

/// <summary>Moves a work order along its lifecycle. The permission depends on the target status (assign / complete / close / cancel).</summary>
public sealed record ChangeWorkOrderStatus(Guid Id, WorkOrderStatus Status, decimal? ActualCost = null, string? ResolutionNotes = null) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => Status switch
    {
        WorkOrderStatus.Assigned => P.WorkOrdersAssign,
        WorkOrderStatus.Completed => P.WorkOrdersComplete,
        WorkOrderStatus.Closed => P.WorkOrdersClose,
        WorkOrderStatus.Cancelled => P.WorkOrdersCancel,
        _ => P.WorkOrdersEdit,
    };
}

public sealed class WorkOrderInputValidator : AbstractValidator<WorkOrderInput>
{
    public WorkOrderInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Issue).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category).MaximumLength(50);
    }
}

public sealed class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrder>
{
    public CreateWorkOrderValidator() => RuleFor(x => x.Input).SetValidator(new WorkOrderInputValidator());
}

public sealed class UpdateWorkOrderValidator : AbstractValidator<UpdateWorkOrder>
{
    public UpdateWorkOrderValidator() => RuleFor(x => x.Input).SetValidator(new WorkOrderInputValidator());
}

public sealed class ChangeWorkOrderStatusValidator : AbstractValidator<ChangeWorkOrderStatus>
{
    public ChangeWorkOrderStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0).When(x => x.ActualCost is not null);
    }
}

internal sealed class WorkOrderHandlers(
    IRepository<WorkOrder> workOrders, IReadRepository<Property> properties, IRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<Party> parties,
    INumberingService numbers, ICustomFieldService fields, IUnitOfWork unitOfWork, IClock clock) :
    IRequestHandler<GetWorkOrder, WorkOrderDto>,
    IRequestHandler<CreateWorkOrder, WorkOrderDto>,
    IRequestHandler<UpdateWorkOrder, WorkOrderDto>,
    IRequestHandler<ChangeWorkOrderStatus, WorkOrderDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<WorkOrderDto> Handle(GetWorkOrder q, CancellationToken ct)
    {
        var workOrder = await workOrders.GetAsync(q.Id, ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }

    public async Task<WorkOrderDto> Handle(CreateWorkOrder cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var workOrder = new WorkOrder { LegalEntityId = property.LegalEntityId, ReportedOn = i.ReportedOn ?? Today };
        await MapAsync(i, workOrder, ct);
        workOrder.Number = await numbers.NextAsync(Docs.WorkOrder, workOrder.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.WorkOrder, workOrder, i.CustomFields, isCreate: true, ct);
        workOrders.Add(workOrder);
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }

    public async Task<WorkOrderDto> Handle(UpdateWorkOrder cmd, CancellationToken ct)
    {
        var workOrder = await workOrders.GetForUpdateAsync(cmd.Id, ct);
        if (!workOrder.IsOpen) throw new ConflictException($"A {workOrder.Status} work order can no longer be edited.");
        if (cmd.Input.PropertyId != workOrder.PropertyId) throw new FieldValidationException("propertyId", "A work order cannot move to another property.");
        await MapAsync(cmd.Input, workOrder, ct);
        await fields.ApplyAsync(Docs.WorkOrder, workOrder, cmd.Input.CustomFields, isCreate: false, ct);
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }

    public Task<WorkOrderDto> Handle(ChangeWorkOrderStatus cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var workOrder = await workOrders.GetForUpdateAsync(cmd.Id, token);
        if (cmd.ActualCost is { } actual) workOrder.ActualCost = actual;
        workOrder.MoveTo(cmd.Status, Today, cmd.ResolutionNotes);

        // Spec 4.1: a unit parked as Unavailable for repairs returns to Vacant when its last open work order is done.
        if (!workOrder.IsOpen && workOrder.UnitId is { } unitId)
        {
            var unit = await units.GetForUpdateAsync(unitId, token);
            var othersOpen = await workOrders.AnyAsync(w => w.UnitId == unitId && w.Id != workOrder.Id
                && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), token);
            if (!othersOpen && unit.Status == UnitStatus.Unavailable && unit.UnavailableReason?.StartsWith("Repairs pending", StringComparison.Ordinal) == true)
                unit.MakeAvailable(Today);
        }
        await workOrders.SaveChangesAsync(token);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, token));
    }, ct);

    private async Task MapAsync(WorkOrderInput i, WorkOrder w, CancellationToken ct)
    {
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct))
            throw new FieldValidationException("unitId", "That unit does not belong to the property.");
        if (i.LeaseId is { } leaseId && !await leases.AnyAsync(l => l.Id == leaseId && l.PropertyId == i.PropertyId, ct))
            throw new FieldValidationException("leaseId", "That lease does not belong to the property.");
        if (i.VendorPartyId is { } vendorId && !await parties.AnyAsync(p => p.Id == vendorId, ct))
            throw new FieldValidationException("vendorPartyId", "Vendor not found.");
        w.PropertyId = i.PropertyId; w.UnitId = i.UnitId; w.LeaseId = i.LeaseId; w.Issue = i.Issue.Trim(); w.Category = i.Category; w.Priority = i.Priority;
        if (i.ReportedOn is { } reported) w.ReportedOn = reported;
        w.ReportedBy = i.ReportedBy; w.VendorPartyId = i.VendorPartyId; w.Technician = i.Technician; w.ScheduledFor = i.ScheduledFor;
        w.EstimatedCost = i.EstimatedCost; w.Details = i.Details;
    }
}
