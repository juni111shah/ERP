using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class RunBillingHandler(IRentInstalmentRepository instalments, RentInvoicer invoicer, IUnitOfWork uow) : ICommandHandler<RunBillingCommand, BillingResult>
{
    public Task<BillingResult> Handle(RunBillingCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The scheduled instalments due by the date, oldest first.
        var due = await instalments.ListScheduledDueForUpdateAsync(cmd.AsOf, cmd.LegalEntityId, token);

        // 2. One invoice each (instalments of leases that never started are skipped).
        var created = new List<(RentInstalment I, Invoice Inv)>();
        foreach (var i in due)
            if (await invoicer.TryInvoiceAsync(i, token) is { } inv) created.Add((i, inv));

        // 3. Save.
        await instalments.SaveChangesAsync(token);
        return new BillingResult(created.Count, created.Sum(c => c.Inv.Total), created.Select(c => c.Inv.Id).ToList());
    }, ct);
}
