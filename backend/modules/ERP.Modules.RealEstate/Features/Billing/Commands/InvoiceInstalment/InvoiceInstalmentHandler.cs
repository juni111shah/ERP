using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class InvoiceInstalmentHandler(IRepository<RentInstalment> instalments, RentInvoicer invoicer) : ICommandHandler<InvoiceInstalmentCommand, InstalmentInvoiceDto>
{
    public async Task<InstalmentInvoiceDto> Handle(InvoiceInstalmentCommand cmd, CancellationToken ct)
    {
        // 1. Only a scheduled instalment of an activated lease can be invoiced.
        var i = await instalments.GetForUpdateAsync(cmd.InstalmentId, ct);
        if (i.Status != InstalmentStatus.Scheduled) throw new ConflictException("This instalment is already invoiced or cancelled.");
        var inv = await invoicer.TryInvoiceAsync(i, ct) ?? throw new ConflictException("The lease of this instalment was never activated, so it cannot be invoiced.");

        // 2. Save.
        await instalments.SaveChangesAsync(ct);
        return new InstalmentInvoiceDto(i.Id, inv.Id, inv.Number, inv.Total);
    }
}
