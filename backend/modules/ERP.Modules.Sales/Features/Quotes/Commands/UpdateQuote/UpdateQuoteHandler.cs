using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class UpdateQuoteHandler(ISalesQuoteRepository quotes, ICustomFieldService fields, IReferenceCheck references) : ICommandHandler<UpdateQuoteCommand, QuoteDto>
{
    public async Task<QuoteDto> Handle(UpdateQuoteCommand cmd, CancellationToken ct)
    {
        // 1. A converted quote cannot change.
        var quote = await quotes.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        if (quote.Status == SalesQuoteStatus.Converted)
            throw new ConflictException("Cannot edit a converted quote.");

        // 2. The header, with a customer and items of this workspace.
        var i = cmd.Input;
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        quote.CustomerPartyId = i.CustomerPartyId;
        quote.CustomerName = i.CustomerName.Trim();
        quote.CustomerEmail = i.CustomerEmail?.Trim();
        quote.CustomerPhone = i.CustomerPhone?.Trim();
        quote.QuoteDate = i.QuoteDate;
        quote.ValidUntil = i.ValidUntil;
        quote.Currency = i.Currency.Trim().ToUpperInvariant();
        quote.Notes = i.Notes;
        quote.Terms = i.Terms;

        // 3. Lines, totals and custom fields, then save.
        QuoteLines.PopulateLinesAndTotals(quote, i.Lines);
        await fields.ApplyAsync(Docs.Quote, quote, i.CustomFields, isCreate: false, ct);

        await quotes.SaveChangesAsync(ct);
        return await QuoteDto.FromAsync(quote, fields, ct);
    }
}
