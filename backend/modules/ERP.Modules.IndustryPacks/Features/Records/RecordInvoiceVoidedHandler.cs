using ERP.Application.Finance;
using ERP.Modules.IndustryPacks.Repositories;
using MediatR;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>When a billed invoice is voided, the record or its lines can be billed again.</summary>
internal sealed class RecordInvoiceVoidedHandler(IIndustryRecordRepository records) : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "industry.record";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } id) return;
        var record = await records.FindWithLinesForUpdateAsync(id, ct);
        if (record is null) return;
        if (record.InvoiceId == n.InvoiceId) record.InvoiceId = null;
        foreach (var line in record.Lines.Where(l => l.InvoiceId == n.InvoiceId)) line.InvoiceId = null;
    }
}
