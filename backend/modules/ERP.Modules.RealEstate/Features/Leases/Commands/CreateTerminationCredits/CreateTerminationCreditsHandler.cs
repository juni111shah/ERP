using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateTerminationCreditsHandler(
    IReadRepository<Lease> leases, IRentInstalmentRepository instalments, IRealEstateInvoiceRepository invoices, IInvoicingService invoicing, IUnitOfWork uow,
    IRepository<CreditApplication> creditApplications, ICurrentUser user)
    : ICommandHandler<CreateTerminationCreditsCommand, IReadOnlyList<TerminationCreditDto>>
{
    public Task<IReadOnlyList<TerminationCreditDto>> Handle(CreateTerminationCreditsCommand cmd, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<TerminationCreditDto>>(async token =>
    {
        // 1. A terminated lease and its invoiced instalments that reach past the termination date (not credited yet).
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status != LeaseStatus.Terminated || lease.TerminationDate is not { } end) throw new ConflictException("Credit notes are raised for terminated leases.");
        var due = await instalments.ListInvoicedPastDateNotCreditedForUpdateAsync(lease.Id, end, token);

        // 2. One credit note per instalment for the rent after the date.
        var created = new List<TerminationCreditDto>();
        foreach (var i in due)
        {
            var original = await invoices.FirstWithLinesForUpdateAsync(i.InvoiceId, token);
            if (original.Status is DocumentStatus.Voided or DocumentStatus.Draft) continue;
            var periodDays = i.PeriodTo.DayNumber - i.PeriodFrom.DayNumber + 1;
            var unusedDays = i.PeriodFrom > end ? periodDays : i.PeriodTo.DayNumber - end.DayNumber;
            var credit = i.PeriodFrom > end ? original.Net : Money.Round(original.Net * unusedDays / periodDays);
            if (credit <= 0) continue;
            var line = original.Lines.First();
            var description = $"Credit for rent after termination on {end:dd MMM yyyy}: {line.Description}";
            var draft = new InvoiceDraft(original.LegalEntityId, original.PartyId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), [new InvoiceLineDraft(description, 1, credit, line.TaxCodeId, line.AccountId)],
                IsCreditNote: true, SourceType: "realestate.termination", SourceId: i.Id, Notes: lease.Number, CreditNoteForId: original.Id);
            var note = await invoicing.BuildAsync(draft, token);
            await invoicing.IssueAsync(note, token);
            // An unpaid invoice is reduced straight away; a paid one leaves the credit with the customer.
            var applied = false;
            if (original.Status is DocumentStatus.Issued or DocumentStatus.PartlyPaid)
            {
                var use = Math.Min(note.Total, original.Total - original.Paid);
                if (use > 0)
                {
                    note.Paid += use; original.Paid += use; SettlementStatus.Refresh(note); SettlementStatus.Refresh(original); applied = true;
                    CreditApplications.Record(creditApplications, note, original, use, note.InvoiceDate, user.UserId);
                }
            }
            i.CreditNoteId = note.Id;
            created.Add(new TerminationCreditDto(i.Id, note.Id, note.Number, note.Total, applied));
        }

        // 3. Save.
        await instalments.SaveChangesAsync(token);
        return created;
    }, ct);
}
