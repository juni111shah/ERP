using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record BillingResult(int Invoiced, decimal TotalBilled, IReadOnlyList<Guid> InvoiceIds);
public sealed record InstalmentInvoiceDto(Guid InstalmentId, Guid InvoiceId, string Number, decimal Total);

/// <summary>Invoices every scheduled instalment of an active lease that has fallen due by the given date (one invoice per instalment, posted to the ledger).</summary>
public sealed record RunBilling(DateOnly AsOf, Guid? LegalEntityId) : ICommand<BillingResult>, Application.Security.IRequirePermission
{
    public string RequiredPermission => P.BillingRun;
}

/// <summary>Invoices one instalment ahead of its due date.</summary>
public sealed record InvoiceInstalment(Guid InstalmentId) : ICommand<InstalmentInvoiceDto>, Application.Security.IRequirePermission
{
    public string RequiredPermission => P.BillingRun;
}

internal sealed class BillingHandlers(
    IRepository<RentInstalment> instalments, IReadRepository<Lease> leases, IReadRepository<Unit> units, IReadRepository<Property> properties,
    IReadRepository<TaxCode> taxCodes, IInvoicingService invoicing, IPostingService posting, IUnitOfWork uow) :
    IRequestHandler<RunBilling, BillingResult>,
    IRequestHandler<InvoiceInstalment, InstalmentInvoiceDto>,
    INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "realestate.instalment";

    public Task<BillingResult> Handle(RunBilling cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var due = await instalments.Tracked(new Specification<RentInstalment>(i => i.Status == InstalmentStatus.Scheduled && i.DueDate <= cmd.AsOf && (cmd.LegalEntityId == null || i.LegalEntityId == cmd.LegalEntityId)))
            .OrderBy(i => i.DueDate).ThenBy(i => i.Sequence).ToListAsync(token);
        var created = new List<(RentInstalment I, Invoice Inv)>();
        foreach (var i in due)
            if (await TryInvoiceAsync(i, token) is { } inv) created.Add((i, inv));
        await instalments.SaveChangesAsync(token);
        return new BillingResult(created.Count, created.Sum(c => c.Inv.Total), created.Select(c => c.Inv.Id).ToList());
    }, ct);

    public async Task<InstalmentInvoiceDto> Handle(InvoiceInstalment cmd, CancellationToken ct)
    {
        var i = await instalments.GetForUpdateAsync(cmd.InstalmentId, ct);
        if (i.Status != InstalmentStatus.Scheduled) throw new ConflictException("This instalment is already invoiced or cancelled.");
        var inv = await TryInvoiceAsync(i, ct) ?? throw new ConflictException("The lease of this instalment is not active, so it cannot be invoiced.");
        await instalments.SaveChangesAsync(ct);
        return new InstalmentInvoiceDto(i.Id, inv.Id, inv.Number, inv.Total);
    }

    /// <summary>When an invoice made from an instalment is voided, the instalment becomes billable again.</summary>
    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } id) return;
        var i = await instalments.Tracked(new Specification<RentInstalment>(x => x.Id == id && x.InvoiceId == n.InvoiceId)).FirstOrDefaultAsync(ct);
        if (i is null) return;
        i.InvoiceId = null; i.Status = InstalmentStatus.Scheduled;
        await instalments.SaveChangesAsync(ct);
    }

    private async Task<Invoice?> TryInvoiceAsync(RentInstalment i, CancellationToken ct)
    {
        var lease = await leases.FindAsync(i.LeaseId, ct);
        if (lease is null || !lease.IsOccupying) return null;
        var unit = await units.GetAsync(lease.UnitId, ct);
        var property = await properties.GetAsync(lease.PropertyId, ct);
        // Commercial rent is standard-rated unless the lease names a tax code; residential rent is exempt unless it does.
        Guid? taxId = lease.TaxCodeId;
        if (taxId is null && unit.Usage == UnitUsage.Commercial)
            taxId = await taxCodes.Query(new Specification<TaxCode>(t => t.IsDefault && t.IsActive)).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        var income = await posting.AccountForRoleAsync("realestate.income.leasing", ct);
        var description = $"Rent {property.Name} unit {unit.UnitNumber}, {i.PeriodFrom:dd MMM yyyy} to {i.PeriodTo:dd MMM yyyy}";
        var draft = new InvoiceDraft(lease.LegalEntityId, lease.LesseePartyId, i.DueDate, i.DueDate, [new InvoiceLineDraft(description, 1, i.Amount, taxId, income)], SourceType: SourceType, SourceId: i.Id, Notes: lease.Number);
        var invoice = await invoicing.BuildAsync(draft, ct);
        await invoicing.IssueAsync(invoice, ct);
        i.InvoiceId = invoice.Id; i.Status = InstalmentStatus.Invoiced;
        return invoice;
    }
}
