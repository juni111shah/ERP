using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record TaxCodeDto(Guid Id, string Code, string Name, TaxKind Kind, decimal Rate, bool IsDefault, bool IsRecoverable, bool IsActive)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<TaxCode, TaxCodeDto>> Projection =
        t => new TaxCodeDto(t.Id, t.Code, t.Name, t.Kind, t.Rate, t.IsDefault, t.IsRecoverable, t.IsActive);

    public static TaxCodeDto From(TaxCode t) => new(t.Id, t.Code, t.Name, t.Kind, t.Rate, t.IsDefault, t.IsRecoverable, t.IsActive);
}
