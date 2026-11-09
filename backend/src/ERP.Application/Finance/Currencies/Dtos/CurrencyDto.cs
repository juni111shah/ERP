using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record CurrencyDto(string Code, string Name, string? Symbol, int Decimals)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Currency, CurrencyDto>> Projection = c => new CurrencyDto(c.Code, c.Name, c.Symbol, c.Decimals);
}
