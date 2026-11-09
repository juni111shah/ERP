using ERP.Application.Common;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class DeleteQuoteHandler(ISalesQuoteRepository quotes) : ICommandHandler<DeleteQuoteCommand>
{
    public async Task<Unit> Handle(DeleteQuoteCommand cmd, CancellationToken ct)
    {
        var quote = await quotes.GetForUpdateAsync(cmd.Id, ct);
        if (quote.Status == SalesQuoteStatus.Converted)
            throw new ConflictException("Cannot delete a converted quote.");

        quotes.Remove(quote);
        await quotes.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
