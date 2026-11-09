using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class CreateQuoteHandler(
    ISalesQuoteRepository quotes,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IReferenceCheck references) : ICommandHandler<CreateQuoteCommand, QuoteDto>
{
    public async Task<QuoteDto> Handle(CreateQuoteCommand cmd, CancellationToken ct)
    {
        // 1. The header, with a customer and items of this workspace.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        var quote = new SalesQuote
        {
            LegalEntityId = entity.Id,
            CustomerPartyId = i.CustomerPartyId,
            CustomerName = i.CustomerName.Trim(),
            CustomerEmail = i.CustomerEmail?.Trim(),
            CustomerPhone = i.CustomerPhone?.Trim(),
            QuoteDate = i.QuoteDate,
            ValidUntil = i.ValidUntil,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            Notes = i.Notes,
            Terms = i.Terms,
            Status = SalesQuoteStatus.Draft,
        };

        // 2. A free number (the next in the sequence unless one is given).
        quote.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.Quote, entity.Id, i.QuoteDate, ct)
            : i.Number.Trim();

        if (await quotes.AnyAsync(x => x.Number == quote.Number, ct))
            throw new ConflictException($"Quote number '{quote.Number}' already exists.");

        // 3. Lines, totals and custom fields, then save.
        QuoteLines.PopulateLinesAndTotals(quote, i.Lines);
        await fields.ApplyAsync(Docs.Quote, quote, i.CustomFields, isCreate: true, ct);

        quotes.Add(quote);
        await quotes.SaveChangesAsync(ct);
        return await QuoteDto.FromAsync(quote, fields, ct);
    }
}
