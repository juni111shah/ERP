using ERP.Application.Common;
using ERP.Application.Templates;

namespace ERP.Application.Platform;

internal sealed class ListCurrenciesHandler : IQueryHandler<ListCurrenciesQuery, IReadOnlyList<CurrencyOptionDto>>
{
    public Task<IReadOnlyList<CurrencyOptionDto>> Handle(ListCurrenciesQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CurrencyOptionDto>>(TaxTemplates.Currencies.Select(c => new CurrencyOptionDto(c.Code, c.Name, c.Symbol, c.Decimals)).ToList());
}
