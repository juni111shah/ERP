using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class CloseSessionHandler(
    IPosSessionRepository sessions,
    IRepository<PosRegister> registers,
    ICashDrawerTransactionRepository drawerTransactions,
    PosSessionCaller caller,
    IUnitOfWork uow,
    IPostingService posting,
    PosAccounts accounts) : ICommandHandler<CloseSessionCommand, PosSessionDto>
{
    public Task<PosSessionDto> Handle(CloseSessionCommand request, CancellationToken cancellationToken) => uow.InTransactionAsync(async token =>
    {
        // 1. The closing notes are added to the opening ones.
        var callerId = caller.Id;
        var existing = await sessions.FindNotesAsync(request.SessionId, token)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? existing.Notes
            : string.IsNullOrWhiteSpace(existing.Notes) ? request.Notes : $"{existing.Notes} | {request.Notes}";
        var closedAt = DateTimeOffset.UtcNow;
        var actual = request.ActualCash;

        // 2. Close only while open, and take the difference from the committed ExpectedCash in the same statement.
        var rows = await sessions.CloseIfOpenAsync(request.SessionId, callerId, closedAt, actual, notes, token);
        if (rows == 0) throw new ConflictException("Session is already closed.");

        var session = await sessions.FirstAsync(request.SessionId, token);
        session.DrawerTransactions = await drawerTransactions.ListForSessionAsync(session.Id, token);

        // 3. Cash over or short is real money: without this entry the ledger keeps a balance the drawer does not have.
        if (session.CashDifference != 0)
        {
            var register = await registers.FindAsync(session.RegisterId, token) ?? throw new NotFoundException($"Register {session.RegisterId} not found");
            var cash = await accounts.CashAsync(register, token);
            var variance = await accounts.VarianceAsync(register, token);
            var memo = $"Cash {(session.CashDifference > 0 ? "over" : "short")} on closing {session.RegisterName}";
            var amount = Math.Abs(session.CashDifference);
            await posting.PostAsync(new PostingRequest(session.LegalEntityId, DateOnly.FromDateTime(closedAt.UtcDateTime), PosSessionCaller.JournalSource, session.Id, memo, session.RegisterName,
                session.CashDifference > 0
                    ? [new PostingLine(cash, amount, 0, memo), new PostingLine(variance, 0, amount, memo)]
                    : [new PostingLine(variance, amount, 0, memo), new PostingLine(cash, 0, amount, memo)]), token);
            await sessions.SaveChangesAsync(token);
        }
        return PosSessionDto.From(session);
    }, cancellationToken);
}
