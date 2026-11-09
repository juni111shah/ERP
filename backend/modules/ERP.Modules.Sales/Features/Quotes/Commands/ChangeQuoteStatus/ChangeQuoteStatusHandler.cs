using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class ChangeQuoteStatusHandler(ISalesQuoteRepository quotes, ICustomFieldService fields) : ICommandHandler<ChangeQuoteStatusCommand, QuoteDto>
{
    public async Task<QuoteDto> Handle(ChangeQuoteStatusCommand cmd, CancellationToken ct)
    {
        var quote = await quotes.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        SalesTransitions.Quote.Ensure(quote.Status, cmd.Status, isSystem: false);

        quote.Status = cmd.Status;
        await quotes.SaveChangesAsync(ct);
        return await QuoteDto.FromAsync(quote, fields, ct);
    }
}
