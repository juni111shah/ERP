using ERP.Application.Organizations;

namespace ERP.Modules.Retail.Features;

/// <summary>The currency of a price list being created or updated.</summary>
internal sealed class PriceListCurrencies(ILegalEntityGuard guard)
{
    /// <summary>The currency asked for, upper-cased, or the legal entity's base currency.</summary>
    public async Task<string> CurrencyAsync(string? requested, Guid legalEntityId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(requested) ? (await guard.RequireAsync(legalEntityId, ct)).BaseCurrency : requested.Trim().ToUpperInvariant();
}
