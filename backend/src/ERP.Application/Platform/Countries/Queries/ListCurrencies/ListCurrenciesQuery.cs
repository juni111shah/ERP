using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The currencies a workspace can keep its books in.</summary>
public sealed record ListCurrenciesQuery : IQuery<IReadOnlyList<CurrencyOptionDto>>;
