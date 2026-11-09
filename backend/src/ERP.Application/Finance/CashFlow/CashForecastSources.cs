using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>Cheques in custody (by the date on the cheque) and cheques we issued (by their date) are expected to move cash on those dates.</summary>
public sealed class ChequeForecastSource(IReadRepository<PaymentInstrument> instruments) : ICashForecastSource
{
    public string Key => "cheques";
    public string Label => "Cheques";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await instruments.Query(new Specification<PaymentInstrument>(i => (i.Status == InstrumentStatus.InCustody || i.Status == InstrumentStatus.Deposited || i.Status == InstrumentStatus.Issued) && i.InstrumentDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.Direction, i.Amount, i.InstrumentDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.InstrumentDate, r.Direction == InstrumentDirection.Received ? r.Amount : -r.Amount)).ToList();
    }
}

/// <summary>Open invoices by due date (credit notes reduce it). Cheques already received are not counted again because they settled their invoices.</summary>
public sealed class ReceivablesForecastSource(IReadRepository<Invoice> invoices) : ICashForecastSource
{
    public string Key => "receivables";
    public string Label => "Receivables";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.DueDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.IsCreditNote, i.Total, i.Paid, i.DueDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, (r.IsCreditNote ? -1 : 1) * (r.Total - r.Paid))).ToList();
    }
}

public sealed class PayablesForecastSource(IReadRepository<Bill> bills) : ICashForecastSource
{
    public string Key => "bills";
    public string Label => "Bills";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.DueDate <= horizon && (entityId == null || b.LegalEntityId == entityId)))
            .Select(b => new { b.Total, b.Paid, b.DueDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, -(r.Total - r.Paid))).ToList();
    }
}
