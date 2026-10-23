using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public enum RenewalState { Due, DraftRenewal, Renewed }

public sealed record RenewalRow(Guid LeaseId, string? Number, string Tenant, string Property, string Unit, DateOnly EndDate, int DaysLeft, decimal AnnualRent, decimal MarketRent, decimal? MarketVariancePercent, string CurrencyCode, RenewalState State, Guid? RenewalLeaseId);
public sealed record RenewalWorkbenchDto(DateOnly AsOf, int WithinDays, IReadOnlyList<RenewalRow> Rows, int Due, int Draft, int Renewed);
public sealed record RenewalDraftResult(Guid LeaseId, Guid? RenewalLeaseId, string? Error);

/// <summary>Active leases ending within the window, with their renewal state and how the rent compares with the unit's asking rent.</summary>
public sealed record GetRenewalWorkbench(int WithinDays, DateOnly AsOf) : IQuery<RenewalWorkbenchDto>, IRequirePermission { public string RequiredPermission => P.LeasesRenew; }
/// <summary>Creates draft renewals for the selected leases, raising the rent by UpliftPercent (0 = same rent).</summary>
public sealed record CreateRenewalDrafts(IReadOnlyList<Guid> LeaseIds, decimal UpliftPercent) : ICommand<IReadOnlyList<RenewalDraftResult>>, IRequirePermission { public string RequiredPermission => P.LeasesRenew; }

public sealed class CreateRenewalDraftsValidator : AbstractValidator<CreateRenewalDrafts>
{
    public CreateRenewalDraftsValidator()
    {
        RuleFor(x => x.LeaseIds).NotEmpty().Must(l => l.Count <= 200).WithMessage("Select at most 200 leases.");
        RuleFor(x => x.UpliftPercent).InclusiveBetween(-50, 100);
    }
}

internal sealed class RenewalHandlers(IReadRepository<Lease> leases, IReadRepository<Party> parties, IReadRepository<Property> properties, IReadRepository<Unit> units, ISender sender) :
    IRequestHandler<GetRenewalWorkbench, RenewalWorkbenchDto>,
    IRequestHandler<CreateRenewalDrafts, IReadOnlyList<RenewalDraftResult>>
{
    public async Task<RenewalWorkbenchDto> Handle(GetRenewalWorkbench q, CancellationToken ct)
    {
        var horizon = q.AsOf.AddDays(q.WithinDays);
        var ending = await leases.Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && l.EndDate <= horizon)).OrderBy(l => l.EndDate).ToListAsync(ct);
        var ids = ending.Select(l => l.Id).ToList();
        var renewals = await leases.Query(new Specification<Lease>(l => l.RenewalOfLeaseId != null && ids.Contains(l.RenewalOfLeaseId.Value) && l.Status != LeaseStatus.Cancelled))
            .Select(l => new { l.Id, Of = l.RenewalOfLeaseId!.Value, l.Status }).ToListAsync(ct);
        var partyIds = ending.Select(l => l.LesseePartyId).Distinct().ToList();
        var propertyIds = ending.Select(l => l.PropertyId).Distinct().ToList();
        var unitIds = ending.Select(l => l.UnitId).Distinct().ToList();
        var tenants = await parties.Query(new Specification<Party>(p => partyIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var props = await properties.Query(new Specification<Property>(p => propertyIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var unitMap = await units.Query(new Specification<Unit>(u => unitIds.Contains(u.Id))).Select(u => new { u.Id, u.UnitNumber, u.AskingRent }).ToDictionaryAsync(u => u.Id, ct);

        var rows = ending.Select(l =>
        {
            var renewal = renewals.FirstOrDefault(r => r.Of == l.Id);
            var state = renewal is null ? RenewalState.Due : renewal.Status == LeaseStatus.Draft ? RenewalState.DraftRenewal : RenewalState.Renewed;
            var unit = unitMap.GetValueOrDefault(l.UnitId);
            var market = unit?.AskingRent ?? 0m;
            return new RenewalRow(l.Id, l.Number, tenants.GetValueOrDefault(l.LesseePartyId, ""), props.GetValueOrDefault(l.PropertyId, ""), unit?.UnitNumber ?? "", l.EndDate, l.EndDate.DayNumber - q.AsOf.DayNumber,
                l.AnnualRent, market, market > 0 && l.AnnualRent > 0 ? Math.Round((market - l.AnnualRent) / l.AnnualRent * 100, 1) : null, l.CurrencyCode, state, renewal?.Id);
        }).ToList();
        return new(q.AsOf, q.WithinDays, rows, rows.Count(r => r.State == RenewalState.Due), rows.Count(r => r.State == RenewalState.DraftRenewal), rows.Count(r => r.State == RenewalState.Renewed));
    }

    public async Task<IReadOnlyList<RenewalDraftResult>> Handle(CreateRenewalDrafts cmd, CancellationToken ct)
    {
        var results = new List<RenewalDraftResult>();
        foreach (var id in cmd.LeaseIds.Distinct())
        {
            try
            {
                var lease = await leases.GetAsync(id, ct);
                var rent = Math.Round(lease.AnnualRent * (1 + cmd.UpliftPercent / 100m), 2);
                var created = await sender.Send(new RenewLease(id, new RenewalInput(null, rent, null, null, cmd.UpliftPercent == 0 ? null : $"Renewal with {cmd.UpliftPercent:0.##}% rent change")), ct);
                results.Add(new(id, created.Id, null));
            }
            catch (Exception e) when (e is ConflictException or FieldValidationException or NotFoundException)
            {
                results.Add(new(id, null, e.Message));
            }
        }
        return results;
    }
}
