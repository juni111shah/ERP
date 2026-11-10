using ERP.Application.Templates;

namespace ERP.Application.Finance;

public sealed record TaxReturnDto(string CountryCode, string Code, string Title, string TitleAr, DateOnly From, DateOnly To, IReadOnlyList<TaxReturnRow> Rows, decimal NetPayable, string CurrencyCode, string? Notes);
