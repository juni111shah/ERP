using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class RenewLeaseHandler(IRepository<Lease> leases, LeaseDtoFactory factory, INumberingService numbers, ICustomFieldService fields, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<RenewLeaseCommand, LeaseDto>
{
    public Task<LeaseDto> Handle(RenewLeaseCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(token => RenewCoreAsync(cmd, token), ct);

    private async Task<LeaseDto> RenewCoreAsync(RenewLeaseCommand cmd, CancellationToken ct)
    {
        // 1. Only an active lease without a renewal can be renewed.
        var old = await leases.GetAsync(cmd.Id, ct);
        if (!old.IsOccupying) throw new ConflictException($"Only an active lease can be renewed (this lease is {old.Status}).");
        if (await leases.AnyAsync(l => l.RenewalOfLeaseId == old.Id && l.Status != LeaseStatus.Cancelled, ct))
            throw new ConflictException("This lease already has a renewal.");

        // 2. The renewal starts the day after the old lease ends; the form overrides its terms.
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

        // 3. Number it, apply the custom field defaults, save.
        renewal.Number = await numbers.NextAsync(Docs.Lease, renewal.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Lease, renewal, null, isCreate: true, ct);
        leases.Add(renewal);
        await leases.SaveChangesAsync(ct);
        return await factory.BuildAsync(renewal, ct);
    }
}
