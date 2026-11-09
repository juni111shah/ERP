using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Raises and issues the invoice of one rent instalment (shared by the billing run and invoicing one instalment).</summary>
internal sealed class RentInvoicer(
    IReadRepository<Lease> leases, IReadRepository<Unit> units, IReadRepository<Property> properties, IRealEstateTaxCodeRepository taxCodes,
    IInvoicingService invoicing, IPostingService posting, OwnerAccounts ownerAccounts)
{
    public const string SourceType = "realestate.instalment";

    public async Task<Invoice?> TryInvoiceAsync(RentInstalment i, CancellationToken ct)
    {
        var lease = await leases.FindAsync(i.LeaseId, ct);
        // A terminated, ended or renewed lease still owes the instalments it was scheduled with (termination pro-rates the last one); only drafts and cancelled leases never bill.
        if (lease is null || lease.Status is LeaseStatus.Draft or LeaseStatus.Cancelled) return null;
        var unit = await units.GetAsync(lease.UnitId, ct);
        var property = await properties.GetAsync(lease.PropertyId, ct);
        // Commercial rent is standard-rated unless the lease names a tax code; residential rent is exempt unless it does.
        Guid? taxId = lease.TaxCodeId;
        if (taxId is null && unit.Usage == UnitUsage.Commercial)
            taxId = await taxCodes.FindActiveDefaultIdAsync(ct);
        // rent of a property managed for an owner is the owner's money until the payout; the management fee becomes income there
        var toOwner = property.RentCreditsOwner;
        var income = toOwner ? await ownerAccounts.PayableAsync(ct) : await posting.AccountForRoleAsync("realestate.income.leasing", ct);
        var description = $"Rent {property.Name} unit {unit.UnitNumber}, {i.PeriodFrom:dd MMM yyyy} to {i.PeriodTo:dd MMM yyyy}";
        var draft = new InvoiceDraft(lease.LegalEntityId, lease.LesseePartyId, i.DueDate, i.DueDate, [new InvoiceLineDraft(description, 1, i.Amount, taxId, income)], SourceType: SourceType, SourceId: i.Id, Notes: lease.Number);
        var invoice = await invoicing.BuildAsync(draft, ct);
        await invoicing.IssueAsync(invoice, ct);
        i.InvoiceId = invoice.Id; i.Status = InstalmentStatus.Invoiced; i.CreditedToOwner = toOwner;
        return invoice;
    }
}
