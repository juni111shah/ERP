using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class GetGratuityReportHandler(GratuityLiability liability) : IQueryHandler<GetGratuityReportQuery, GratuityReportDto>
{
    public async Task<GratuityReportDto> Handle(GetGratuityReportQuery query, CancellationToken ct)
    {
        var (rows, rule) = await liability.BuildAsync(query.AsOf, query.LegalEntityId, ct);
        return new GratuityReportDto(query.AsOf, rows, rows.Sum(r => r.Gratuity), rule);
    }
}
