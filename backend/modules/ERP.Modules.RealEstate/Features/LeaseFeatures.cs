using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record LeaseInput(
    Guid UnitId, Guid LesseePartyId, DateOnly StartDate, DateOnly EndDate, decimal AnnualRent, int PaymentFrequency = 4,
    decimal SecurityDepositAmount = 0, Guid? TaxCodeId = null, int GraceDays = 0, int NoticePeriodDays = 90, string? Notes = null,
    JsonObject? CustomFields = null, bool ActivateNow = false);

public sealed record RenewalInput(DateOnly? EndDate, decimal? AnnualRent, int? PaymentFrequency, decimal? SecurityDepositAmount, string? Notes);

public sealed record InstalmentDto(Guid Id, int Sequence, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueDate, decimal Amount, InstalmentStatus Status, Guid? InvoiceId)
{
    public static InstalmentDto From(RentInstalment i) => new(i.Id, i.Sequence, i.PeriodFrom, i.PeriodTo, i.DueDate, i.Amount, i.Status, i.InvoiceId);
}

public sealed record LeaseDto(
    Guid Id, string? Number, Guid UnitId, string UnitNumber, Guid PropertyId, Guid LegalEntityId, Guid LesseePartyId, string TenantName, DateOnly StartDate, DateOnly EndDate,
    decimal AnnualRent, string CurrencyCode, int PaymentFrequency, decimal SecurityDepositAmount, Guid? TaxCodeId, string SuggestedVat, LeaseStatus Status,
    DateTime? ActivatedAt, DateOnly? TerminationDate, string? TerminationReason, Guid? RenewalOfLeaseId, Guid? RenewedAsLeaseId, int GraceDays,
    int NoticePeriodDays, bool HoldingOverFlag, string? Notes, decimal RentForTerm, int? DaysToExpiry, JsonObject CustomFields,
    IReadOnlyList<InstalmentDto> Schedule);

/// <summary>A row of the leases list (spec RE-LSE-12).</summary>
public sealed record LeaseListItem(
    Guid Id, string? Number, Guid UnitId, string UnitNumber, Guid PropertyId, string PropertyName, Guid LesseePartyId, string TenantName,
    DateOnly StartDate, DateOnly EndDate, int? DaysToExpiry, decimal AnnualRent, int PaymentFrequency, decimal SecurityDepositAmount, LeaseStatus Status);

public sealed record TerminationResultDto(LeaseDto Lease, int CancelledInstalments, IReadOnlyList<InstalmentDto> InstalmentsNeedingCreditNote);
public sealed record ExpiryRunResultDto(int Ended, int HeldOver, int ReservationsReleased);

public sealed record ListLeases : PagedQuery, IQuery<PagedResult<LeaseListItem>>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRead;
}

public sealed record GetLease(Guid Id) : IQuery<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRead;
}

public sealed record CreateLease(LeaseInput Input) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => Input.ActivateNow ? P.LeasesActivate : P.LeasesCreate;
}

public sealed record UpdateLease(Guid Id, LeaseInput Input) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}

public sealed record ActivateLease(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesActivate;
}

public sealed record RenewLease(Guid Id, RenewalInput Input) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRenew;
}

public sealed record TerminateLease(Guid Id, DateOnly Date, string Reason) : ICommand<TerminationResultDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesTerminate;
}

public sealed record CancelLease(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}

/// <summary>Marks that the tenant stays after the end date (the nightly job then holds the lease over instead of ending it).</summary>
public sealed record FlagHoldingOver(Guid Id, bool HoldingOver) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}

public sealed record ConfirmLeaseMoveOut(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesTerminate;
}

/// <summary>Daily job body for the current workspace: ends or holds over expired leases and releases expired reservations. Idempotent.</summary>
public sealed record ProcessLeaseExpiries : ICommand<ExpiryRunResultDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesActivate;
}

public sealed class LeaseInputValidator : AbstractValidator<LeaseInput>
{
    public LeaseInputValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.LesseePartyId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The end date must be after the start date.");
        RuleFor(x => x.AnnualRent).GreaterThan(0).WithMessage("Annual rent must be greater than zero.");
        RuleFor(x => x.PaymentFrequency).Must(f => Lease.PaymentPlans.Contains(f)).WithMessage("Payment plan must be 1, 2, 4, 6 or 12 instalments a year.");
        RuleFor(x => x.SecurityDepositAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NoticePeriodDays).InclusiveBetween(0, 365);
        RuleFor(x => x).Must(x => x.StartDate.AddDays(x.GraceDays) <= x.EndDate).WithName("graceDays").WithMessage("The rent-free period must be shorter than the lease term.");
    }
}

public sealed class CreateLeaseValidator : AbstractValidator<CreateLease>
{
    public CreateLeaseValidator() => RuleFor(x => x.Input).SetValidator(new LeaseInputValidator());
}

public sealed class UpdateLeaseValidator : AbstractValidator<UpdateLease>
{
    public UpdateLeaseValidator() => RuleFor(x => x.Input).SetValidator(new LeaseInputValidator());
}

public sealed class TerminateLeaseValidator : AbstractValidator<TerminateLease>
{
    public TerminateLeaseValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("A termination reason is required.");
}

/// <summary>Builds lease DTOs (shared by the query and command handlers).</summary>
internal sealed class LeaseDtoFactory(IReadRepository<RentInstalment> instalments, IReadRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<Party> parties, ICustomFieldService fields, IClock clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<LeaseDto> BuildAsync(Lease lease, CancellationToken ct)
    {
        var unit = await units.GetAsync(lease.UnitId, ct);
        var tenantName = (await parties.FindAsync(lease.LesseePartyId, ct))?.Name ?? "";
        var schedule = await instalments.Query(new Specification<RentInstalment>(i => i.LeaseId == lease.Id)).OrderBy(i => i.Sequence).ToListAsync(ct);
        var renewedAs = await leases.Query(new Specification<Lease>(l => l.RenewalOfLeaseId == lease.Id && l.Status != LeaseStatus.Cancelled)).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
        var rentForTerm = schedule.Where(i => i.Status != InstalmentStatus.Cancelled).Sum(i => i.Amount);
        if (schedule.Count == 0 && lease.Status == LeaseStatus.Draft) // a draft shows the schedule it would get
            rentForTerm = RentSchedule.TermRent(RentSchedule.Generate(lease.StartDate, lease.EndDate, lease.AnnualRent, lease.PaymentFrequency, lease.GraceDays));
        return new(
            lease.Id, lease.Number, lease.UnitId, unit.UnitNumber, lease.PropertyId, lease.LegalEntityId, lease.LesseePartyId, tenantName, lease.StartDate, lease.EndDate,
            lease.AnnualRent, lease.CurrencyCode, lease.PaymentFrequency, lease.SecurityDepositAmount, lease.TaxCodeId, SuggestedVat(lease, unit), lease.Status,
            lease.ActivatedAt, lease.TerminationDate, lease.TerminationReason, lease.RenewalOfLeaseId, renewedAs, lease.GraceDays, lease.NoticePeriodDays,
            lease.HoldingOverFlag, lease.Notes, rentForTerm, DaysToExpiry(lease), await fields.ReadAsync(Docs.Lease, lease, ct), schedule.Select(InstalmentDto.From).ToList());
    }

    /// <summary>Spec RE-BR-VAT-01/02: commercial units are standard-rated, residential exempt, unless the lease overrides it with a tax code.</summary>
    public static string SuggestedVat(Lease lease, Unit unit) => lease.TaxCodeId is not null ? "override" : unit.Usage == UnitUsage.Commercial ? "standard" : "exempt";

    public int? DaysToExpiry(Lease lease) => lease.IsOccupying ? lease.EndDate.DayNumber - Today.DayNumber : null;
}

internal sealed class LeaseQueryHandlers(
    IReadRepository<Lease> leases, IReadRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<Party> parties, LeaseDtoFactory factory) :
    IRequestHandler<ListLeases, PagedResult<LeaseListItem>>,
    IRequestHandler<GetLease, LeaseDto>
{
    public async Task<PagedResult<LeaseListItem>> Handle(ListLeases q, CancellationToken ct)
    {
        var page = await leases.PageAsync(q, ct: ct);
        var unitIds = page.Items.Select(l => l.UnitId).Distinct().ToList();
        var propertyIds = page.Items.Select(l => l.PropertyId).Distinct().ToList();
        var tenantIds = page.Items.Select(l => l.LesseePartyId).Distinct().ToList();
        var unitNumbers = await units.Query(new Specification<Unit>(u => unitIds.Contains(u.Id))).ToDictionaryAsync(u => u.Id, u => u.UnitNumber, ct);
        var propertyNames = await properties.Query(new Specification<Property>(p => propertyIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var tenantNames = await parties.Query(new Specification<Party>(p => tenantIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return page.Map(l => new LeaseListItem(
            l.Id, l.Number, l.UnitId, unitNumbers.GetValueOrDefault(l.UnitId, ""), l.PropertyId, propertyNames.GetValueOrDefault(l.PropertyId, ""), l.LesseePartyId,
            tenantNames.GetValueOrDefault(l.LesseePartyId, ""), l.StartDate, l.EndDate, factory.DaysToExpiry(l), l.AnnualRent, l.PaymentFrequency,
            l.SecurityDepositAmount, l.Status));
    }

    public async Task<LeaseDto> Handle(GetLease q, CancellationToken ct) => await factory.BuildAsync(await leases.GetAsync(q.Id, ct), ct);
}

internal sealed class LeaseCommandHandlers(
    IRepository<Lease> leases, IRepository<RentInstalment> instalments, IRepository<Unit> units, IReadRepository<Property> properties,
    IReadRepository<Party> parties, IReadRepository<LegalEntity> legalEntities, IReadRepository<WorkOrder> workOrders, INumberingService numbers,
    ICustomFieldService fields, IUnitOfWork unitOfWork, ICurrentUser user, IClock clock, LeaseDtoFactory factory) :
    IRequestHandler<CreateLease, LeaseDto>,
    IRequestHandler<UpdateLease, LeaseDto>,
    IRequestHandler<ActivateLease, LeaseDto>,
    IRequestHandler<RenewLease, LeaseDto>,
    IRequestHandler<TerminateLease, TerminationResultDto>,
    IRequestHandler<CancelLease, LeaseDto>,
    IRequestHandler<FlagHoldingOver, LeaseDto>,
    IRequestHandler<ConfirmLeaseMoveOut, LeaseDto>,
    IRequestHandler<ProcessLeaseExpiries, ExpiryRunResultDto>
{
    private DateOnly Today => factory.Today;

    public async Task<LeaseDto> Handle(CreateLease cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var (unit, property, _, legalEntity) = await LoadContextAsync(i, ct);
        if (unit.Status is not (UnitStatus.Vacant or UnitStatus.Reserved))
            throw new FieldValidationException("unitId", $"Unit {unit.UnitNumber} is {unit.Status}; only vacant or reserved units can be let.");

        var lease = new Lease { LegalEntityId = property.LegalEntityId, CurrencyCode = legalEntity.BaseCurrency };
        Map(i, lease, unit);
        lease.Number = await numbers.NextAsync(Docs.Lease, lease.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Lease, lease, i.CustomFields, isCreate: true, ct);
        leases.Add(lease);
        await leases.SaveChangesAsync(ct);
        return i.ActivateNow ? await ActivateAsync(lease.Id, ct) : await factory.BuildAsync(lease, ct);
    }

    public async Task<LeaseDto> Handle(UpdateLease cmd, CancellationToken ct)
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        if (lease.Status != LeaseStatus.Draft)
            throw new ConflictException("Only a draft lease can be edited. Use an amendment, renewal or termination for an active lease.");
        var (unit, _, _, _) = await LoadContextAsync(cmd.Input, ct);
        Map(cmd.Input, lease, unit);
        await fields.ApplyAsync(Docs.Lease, lease, cmd.Input.CustomFields, isCreate: false, ct);
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }

    public Task<LeaseDto> Handle(ActivateLease cmd, CancellationToken ct) => ActivateAsync(cmd.Id, ct);

    /// <summary>Spec RE-LSE-03: no overlapping active lease on the unit; unit -> Occupied (or the renewed lease -> Renewed); schedule fixed.</summary>
    private Task<LeaseDto> ActivateAsync(Guid id, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var lease = await leases.GetForUpdateAsync(id, token);
        if (lease.Status != LeaseStatus.Draft) throw new ConflictException($"Only a draft lease can be activated (this lease is {lease.Status}).");
        var unit = await units.GetForUpdateAsync(lease.UnitId, token);

        var overlapping = await leases.Query(new Specification<Lease>(l =>
                l.Id != lease.Id && l.UnitId == lease.UnitId && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver)
                && l.StartDate <= lease.EndDate && l.EndDate >= lease.StartDate && l.Id != lease.RenewalOfLeaseId))
            .Select(l => l.Number).FirstOrDefaultAsync(token);
        if (overlapping is not null)
            throw new ConflictException($"Unit {unit.UnitNumber} already has an active lease ({overlapping}) for that period.");

        try
        {
            if (lease.RenewalOfLeaseId is { } oldId)
            {
                // The old lease must leave the "occupying" set first: only one occupying lease per unit may exist at any time.
                var old = await leases.GetForUpdateAsync(oldId, token);
                old.MarkRenewed();
                await leases.SaveChangesAsync(token);
            }
            else unit.Occupy();

            lease.Activate(clock.UtcNow, user.UserId);
            instalments.AddRange(RentSchedule.Generate(lease.StartDate, lease.EndDate, lease.AnnualRent, lease.PaymentFrequency, lease.GraceDays)
                .Select(s => new RentInstalment
                {
                    LeaseId = lease.Id, LegalEntityId = lease.LegalEntityId, Sequence = s.Sequence, PeriodFrom = s.PeriodFrom, PeriodTo = s.PeriodTo,
                    DueDate = s.DueDate, Amount = s.Amount,
                }));
            await leases.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"Unit {unit.UnitNumber} was just let by someone else.");
        }
        return await factory.BuildAsync(lease, token);
    }, ct);

    public async Task<LeaseDto> Handle(RenewLease cmd, CancellationToken ct)
    {
        var old = await leases.GetAsync(cmd.Id, ct);
        if (!old.IsOccupying) throw new ConflictException($"Only an active lease can be renewed (this lease is {old.Status}).");
        if (await leases.AnyAsync(l => l.RenewalOfLeaseId == old.Id && l.Status != LeaseStatus.Cancelled, ct))
            throw new ConflictException("This lease already has a renewal.");

        var i = cmd.Input;
        var start = old.EndDate.AddDays(1);
        var renewal = new Lease
        {
            LegalEntityId = old.LegalEntityId, CurrencyCode = old.CurrencyCode, UnitId = old.UnitId, PropertyId = old.PropertyId, LesseePartyId = old.LesseePartyId,
            StartDate = start, EndDate = i.EndDate ?? start.AddYears(1).AddDays(-1), AnnualRent = i.AnnualRent ?? old.AnnualRent,
            PaymentFrequency = i.PaymentFrequency ?? old.PaymentFrequency, SecurityDepositAmount = i.SecurityDepositAmount ?? old.SecurityDepositAmount,
            TaxCodeId = old.TaxCodeId, NoticePeriodDays = old.NoticePeriodDays, RenewalOfLeaseId = old.Id, Notes = i.Notes,
        };
        if (renewal.EndDate <= renewal.StartDate) throw new FieldValidationException("endDate", "The renewal must end after it starts.");
        if (renewal.AnnualRent <= 0) throw new FieldValidationException("annualRent", "Annual rent must be greater than zero.");
        if (!Lease.PaymentPlans.Contains(renewal.PaymentFrequency)) throw new FieldValidationException("paymentFrequency", "Payment plan must be 1, 2, 4, 6 or 12 instalments a year.");
        renewal.Number = await numbers.NextAsync(Docs.Lease, renewal.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Lease, renewal, null, isCreate: true, ct);
        leases.Add(renewal);
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(renewal, ct);
    }

    /// <summary>
    /// Spec RE-LSE-10: cancels instalments that start after the date, pro-rates the instalment the date falls in, lists invoiced
    /// instalments that need a credit note (credit notes arrive with Finance), and vacates the unit.
    /// </summary>
    public Task<TerminationResultDto> Handle(TerminateLease cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, token);
        lease.Terminate(cmd.Date, cmd.Reason);

        var schedule = await instalments.Tracked(new Specification<RentInstalment>(i => i.LeaseId == lease.Id && i.Status != InstalmentStatus.Cancelled)).ToListAsync(token);
        var cancelled = 0;
        var needCredit = new List<RentInstalment>();
        foreach (var i in schedule.Where(i => i.PeriodTo > cmd.Date))
        {
            if (i.Status == InstalmentStatus.Invoiced) { needCredit.Add(i); continue; }
            if (i.PeriodFrom > cmd.Date) { i.Status = InstalmentStatus.Cancelled; cancelled++; continue; }
            // The date falls inside this period: bill only the days used.
            var periodDays = i.PeriodTo.DayNumber - i.PeriodFrom.DayNumber + 1;
            var usedDays = cmd.Date.DayNumber - i.PeriodFrom.DayNumber + 1;
            i.Amount = Math.Round(i.Amount * usedDays / periodDays, 2, MidpointRounding.AwayFromZero);
            i.PeriodTo = cmd.Date;
        }

        var unit = await units.GetForUpdateAsync(lease.UnitId, token);
        var repairsPending = await workOrders.AnyAsync(w => w.UnitId == unit.Id
            && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), token);
        if (unit.Status == UnitStatus.Occupied) unit.Vacate(Today, repairsPending);

        await leases.SaveChangesAsync(token);
        return new TerminationResultDto(await factory.BuildAsync(lease, token), cancelled, needCredit.Select(InstalmentDto.From).ToList());
    }, ct);

    public async Task<LeaseDto> Handle(CancelLease cmd, CancellationToken ct)
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        lease.Cancel();
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }

    public async Task<LeaseDto> Handle(FlagHoldingOver cmd, CancellationToken ct)
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, ct);
        if (!lease.IsOccupying) throw new ConflictException("Only an active lease can be flagged as holding over.");
        lease.HoldingOverFlag = cmd.HoldingOver;
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(lease, ct);
    }

    public Task<LeaseDto> Handle(ConfirmLeaseMoveOut cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var lease = await leases.GetForUpdateAsync(cmd.Id, token);
        lease.ConfirmMoveOut();
        var unit = await units.GetForUpdateAsync(lease.UnitId, token);
        if (unit.Status == UnitStatus.Occupied) unit.Vacate(Today);
        await leases.SaveChangesAsync(token);
        return await factory.BuildAsync(lease, token);
    }, ct);

    public Task<ExpiryRunResultDto> Handle(ProcessLeaseExpiries cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var today = Today;
        int ended = 0, heldOver = 0, released = 0;
        var expired = await leases.Tracked(new Specification<Lease>(l => l.Status == LeaseStatus.Active && l.EndDate < today)).ToListAsync(token);
        foreach (var lease in expired)
        {
            if (lease.Expire(today) == LeaseStatus.HoldingOver) { heldOver++; continue; }
            var unit = await units.GetForUpdateAsync(lease.UnitId, token);
            if (unit.Status == UnitStatus.Occupied) unit.Vacate(today);
            ended++;
        }
        var lapsed = await units.Tracked(new Specification<Unit>(u => u.Status == UnitStatus.Reserved && u.ReservedUntil != null && u.ReservedUntil < today)).ToListAsync(token);
        foreach (var unit in lapsed) { unit.ReleaseReservation(today); released++; }
        await leases.SaveChangesAsync(token);
        return new ExpiryRunResultDto(ended, heldOver, released);
    }, ct);

    /// <summary>Loads and validates everything a lease refers to (unit, its property, the tenant and the owning legal entity).</summary>
    private async Task<(Unit Unit, Property Property, Party Tenant, LegalEntity Entity)> LoadContextAsync(LeaseInput i, CancellationToken ct)
    {
        var unit = await units.FindAsync(i.UnitId, ct) ?? throw new FieldValidationException("unitId", "Unit not found.");
        var property = await properties.GetAsync(unit.PropertyId, ct);
        var tenant = await parties.FindAsync(i.LesseePartyId, ct) ?? throw new FieldValidationException("lesseePartyId", "Tenant not found.");
        var legalEntity = await legalEntities.GetAsync(property.LegalEntityId, ct);
        return (unit, property, tenant, legalEntity);
    }

    private static void Map(LeaseInput i, Lease lease, Unit unit)
    {
        lease.UnitId = unit.Id; lease.PropertyId = unit.PropertyId; lease.LesseePartyId = i.LesseePartyId;
        lease.StartDate = i.StartDate; lease.EndDate = i.EndDate; lease.AnnualRent = i.AnnualRent; lease.PaymentFrequency = i.PaymentFrequency;
        lease.SecurityDepositAmount = i.SecurityDepositAmount; lease.TaxCodeId = i.TaxCodeId; lease.GraceDays = i.GraceDays;
        lease.NoticePeriodDays = i.NoticePeriodDays; lease.Notes = i.Notes;
    }
}
