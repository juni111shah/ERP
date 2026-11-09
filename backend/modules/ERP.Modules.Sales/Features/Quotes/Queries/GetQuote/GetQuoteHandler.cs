using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class GetQuoteHandler(ISalesQuoteRepository quotes, ICustomFieldService fields) : IQueryHandler<GetQuoteQuery, QuoteDto>
{
    public async Task<QuoteDto> Handle(GetQuoteQuery q, CancellationToken ct)
    {
        var quote = await quotes.FindWithLinesForUpdateAsync(q.Id, ct) ?? throw new NotFoundException($"Quote '{q.Id}' not found.");
        return await QuoteDto.FromAsync(quote, fields, ct);
    }
}
