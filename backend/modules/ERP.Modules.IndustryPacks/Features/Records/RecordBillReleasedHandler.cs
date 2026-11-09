using ERP.Application.Finance;
using ERP.Modules.IndustryPacks.Repositories;
using MediatR;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>When a vendor bill made from a record is voided or deleted, the record can be billed again.</summary>
internal sealed class RecordBillReleasedHandler(IIndustryRecordRepository records) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var record = await records.FindByBillForUpdateAsync(n.BillId, ct);
        if (record is not null) record.BillId = null;
    }
}
