using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class RecordDrawerTransactionHandler(
    IPosSessionRepository sessions,
    IRepository<PosRegister> registers,
    IRepository<CashDrawerTransaction> drawerTransactions,
    PosSessionCaller caller,
    ICurrentUser user,
    IUnitOfWork uow,
    IPostingService posting,
    ISettingsService settings,
    PosAccounts accounts) : ICommandHandler<RecordDrawerTransactionCommand, CashDrawerTransactionDto>
{
    public Task<CashDrawerTransactionDto> Handle(RecordDrawerTransactionCommand request, CancellationToken cancellationToken) => uow.InTransactionAsync(async token =>
    {
        // 1. Naming the ledger account needs register management; the shift must exist.
        var callerId = caller.Id;
        var amount = request.Amount;
        if (request.AccountId is not null && !user.HasPermission(Permissions.RegistersManage))
            throw new ForbiddenException("Choosing a ledger account for a cash movement needs the register management permission.");
        var session = await sessions.FindAsync(request.SessionId, token)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        var allowNegative = await settings.GetAsync<bool?>("pos.allowNegativeDrawer", session.LegalEntityId, token) ?? false;

        // 2. Move the shift's figures in one statement, only while it is open (and, for cash out, while the drawer covers it).
        var rows = request.Type switch
        {
            CashMovementType.FloatIn or CashMovementType.PayIn => await sessions.AddCashInAsync(request.SessionId, amount, token),
            CashMovementType.PayOut or CashMovementType.DropOut => await sessions.TakeCashOutAsync(request.SessionId, amount, allowNegative, token),
            _ => 0,
        };
        if (rows == 0)
        {
            if (session.Status != PosSessionStatus.Open || !await sessions.AnyAsync(s => s.Id == session.Id && s.Status == PosSessionStatus.Open, token))
                throw new ConflictException("Cannot record drawer transactions on a closed session.");
            throw new ConflictException($"The drawer does not hold {amount:N2} in cash.");
        }

        // 3. Post against the other side, when there is one.
        var register = await registers.FindAsync(session.RegisterId, token) ?? throw new NotFoundException($"Register {session.RegisterId} not found");
        var counter = request.Type is CashMovementType.PayIn or CashMovementType.PayOut
            ? request.AccountId ?? await accounts.DrawerOffsetAsync(register, token)
            : request.AccountId;

        var reason = request.Reason.Trim();
        if (counter is { } other)
        {
            var cash = await accounts.CashAsync(register, token);
            var cashComesIn = request.Type is CashMovementType.FloatIn or CashMovementType.PayIn;
            var memo = $"Drawer {request.Type} - {session.RegisterName}: {reason}";
            await posting.PostAsync(new PostingRequest(session.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), PosSessionCaller.JournalSource, session.Id, memo.Length > 250 ? memo[..250] : memo, session.RegisterName,
                cashComesIn
                    ? [new PostingLine(cash, amount, 0, reason), new PostingLine(other, 0, amount, reason)]
                    : [new PostingLine(other, amount, 0, reason), new PostingLine(cash, 0, amount, reason)]), token);
        }

        // 4. Record the movement.
        var tx = new CashDrawerTransaction
        {
            SessionId = request.SessionId,
            Type = request.Type,
            Amount = request.Amount,
            Reason = reason,
            PerformedByUserId = callerId,
            PerformedByName = await caller.NameAsync(token),
            Timestamp = DateTimeOffset.UtcNow,
            AccountId = counter,
        };
        drawerTransactions.Add(tx);
        await drawerTransactions.SaveChangesAsync(token);

        return new CashDrawerTransactionDto(tx.Id, tx.SessionId, tx.Type, tx.Amount, tx.Reason, tx.PerformedByUserId, tx.Timestamp, tx.PerformedByName);
    }, cancellationToken);
}
