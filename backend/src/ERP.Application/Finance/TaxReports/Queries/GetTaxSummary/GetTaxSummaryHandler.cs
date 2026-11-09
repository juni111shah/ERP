using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetTaxSummaryHandler(TaxSummaryLineReader lines, ITaxCodeRepository taxCodes, IReportCurrencyGuard currencies)
    : IQueryHandler<GetTaxSummaryQuery, TaxSummaryDto>
{
    public async Task<TaxSummaryDto> Handle(GetTaxSummaryQuery q, CancellationToken ct)
    {
        // 1. The taxed lines of the period, and the tax codes.
        var entity = q.LegalEntityId;
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var sales = await lines.ReadSalesAsync(q.From, q.To, entity, ct);
        var purchases = await lines.ReadPurchasesAsync(q.From, q.To, entity, ct);
        var codes = await taxCodes.MapLabelsAsync(ct);

        TaxSummaryRow Row(string section, Guid? id, decimal net, decimal tax)
            => id is { } k && codes.TryGetValue(k, out var c) ? new(section, k, c.Code, c.Name, c.Rate, net, tax) : new(section, null, "-", "No tax", 0, net, tax);

        // 2. One row per section and tax code (credit notes negative; only recoverable purchase tax counts as input).
        var rows = new List<TaxSummaryRow>();
        rows.AddRange(sales.GroupBy(s => s.TaxCodeId).Select(g => Row("Sales", g.Key, g.Sum(x => x.IsCreditNote ? -x.Net : x.Net), g.Sum(x => x.IsCreditNote ? -x.TaxAmount : x.TaxAmount))));
        rows.AddRange(purchases.Where(p => p.TaxRecoverable).GroupBy(p => p.TaxCodeId).Select(g => Row("Purchases", g.Key, g.Sum(x => x.Net), g.Sum(x => x.TaxAmount))));
        rows = rows.OrderBy(r => r.Section).ThenBy(r => r.Code).ToList();

        // 3. Totals.
        var output = rows.Where(r => r.Section == "Sales").Sum(r => r.Tax); var input = rows.Where(r => r.Section == "Purchases").Sum(r => r.Tax);
        return new TaxSummaryDto(q.From, q.To, rows, output, input, output - input, purchases.Where(p => !p.TaxRecoverable).Sum(p => p.TaxAmount));
    }
}
