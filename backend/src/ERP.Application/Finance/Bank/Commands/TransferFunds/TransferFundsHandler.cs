using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class TransferFundsHandler(CashAccountGuard cashAccounts, IPostingService posting, IUnitOfWork uow) : ICommandHandler<TransferFundsCommand, Guid>
{
    public async Task<Guid> Handle(TransferFundsCommand command, CancellationToken ct)
    {
        // 1. Both sides must be active bank or cash accounts.
        await cashAccounts.RequireAsync(command.FromAccountId, "fromAccountId", ct);
        await cashAccounts.RequireAsync(command.ToAccountId, "toAccountId", ct);

        // 2. Post Dr to, Cr from, and save.
        var memo = command.Memo?.Trim() is { Length: > 0 } m ? m : "Transfer between bank and cash accounts";
        var journal = await posting.PostAsync(new PostingRequest(command.LegalEntityId, command.Date, "transfer", null, memo, null,
            [new PostingLine(command.ToAccountId, command.Amount, 0, memo), new PostingLine(command.FromAccountId, 0, command.Amount, memo)]), ct);
        await uow.SaveChangesAsync(ct);
        return journal.Id;
    }
}
