using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Builds lease DTOs (shared by the query and command handlers).</summary>
internal sealed class LeaseDtoFactory(IRentInstalmentRepository instalments, IReadRepository<Unit> units, ILeaseRepository leases, IReadRepository<Party> parties, ICustomFieldService fields, IClock clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<LeaseDto> BuildAsync(Lease lease, CancellationToken ct)
    {
        var unit = await units.GetAsync(lease.UnitId, ct);
        var tenantName = (await parties.FindAsync(lease.LesseePartyId, ct))?.Name ?? "";
        var schedule = await instalments.ListOfLeaseAsync(lease.Id, ct);
        var renewedAs = await leases.FindRenewalIdAsync(lease.Id, ct);
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
