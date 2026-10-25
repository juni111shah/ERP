using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosSessionSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid RegisterId,
    string RegisterName,
    Guid OpenedByUserId,
    string CashierName,
    DateTimeOffset OpenedAt,
    Guid? ClosedByUserId,
    DateTimeOffset? ClosedAt,
    decimal OpeningCash,
    decimal TotalSales,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal CashDifference,
    PosSessionStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CashDrawerTransactionDto(
    Guid Id,
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    Guid PerformedByUserId,
    DateTimeOffset Timestamp);

public record PosSessionDto(
    Guid Id,
    Guid LegalEntityId,
    Guid RegisterId,
    string RegisterName,
    Guid OpenedByUserId,
    string CashierName,
    DateTimeOffset OpenedAt,
    Guid? ClosedByUserId,
    DateTimeOffset? ClosedAt,
    decimal OpeningCash,
    decimal TotalSales,
    decimal TotalReturns,
    decimal TotalPayIn,
    decimal TotalPayOut,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal CashDifference,
    PosSessionStatus Status,
    string? Notes,
    string? CustomFields,
    List<CashDrawerTransactionDto> DrawerTransactions,
    DateTimeOffset CreatedAt);

public record OpenSessionCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    decimal OpeningCash,
    Guid CashierUserId,
    string CashierName,
    string? Notes) : ICommand<PosSessionDto>;

public record CloseSessionCommand(
    Guid SessionId,
    decimal ActualCash,
    Guid ClosedByUserId,
    string? Notes) : ICommand<PosSessionDto>;

public record RecordDrawerTransactionCommand(
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    Guid PerformedByUserId) : ICommand<CashDrawerTransactionDto>;

public record GetCurrentSessionQuery(Guid RegisterId) : IQuery<PosSessionDto?>;

public class OpenSessionValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.OpeningCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CashierName).NotEmpty();
    }
}

public class CloseSessionValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ActualCash).GreaterThanOrEqualTo(0);
    }
}

public class RecordDrawerTransactionValidator : AbstractValidator<RecordDrawerTransactionCommand>
{
    public RecordDrawerTransactionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public class SessionHandlers :
    IRequestHandler<OpenSessionCommand, PosSessionDto>,
    IRequestHandler<CloseSessionCommand, PosSessionDto>,
    IRequestHandler<RecordDrawerTransactionCommand, CashDrawerTransactionDto>,
    IRequestHandler<GetCurrentSessionQuery, PosSessionDto?>
{
    private readonly IRepository<PosSession> _sessions;
    private readonly IRepository<PosRegister> _registers;
    private readonly IRepository<CashDrawerTransaction> _drawerTransactions;

    public SessionHandlers(
        IRepository<PosSession> sessions,
        IRepository<PosRegister> registers,
        IRepository<CashDrawerTransaction> drawerTransactions)
    {
        _sessions = sessions;
        _registers = registers;
        _drawerTransactions = drawerTransactions;
    }

    public async Task<PosSessionDto> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        var register = await _registers.GetAsync(request.RegisterId, cancellationToken);

        var activeSession = await _sessions.Query(new Specification<PosSession>(
            s => s.RegisterId == request.RegisterId && s.Status == PosSessionStatus.Open))
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSession != null)
        {
            throw new ConflictException($"Register '{register.Name}' already has an active open session.");
        }

        var session = new PosSession
        {
            LegalEntityId = request.LegalEntityId,
            RegisterId = request.RegisterId,
            RegisterName = register.Name,
            OpenedByUserId = request.CashierUserId,
            CashierName = request.CashierName,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningCash = request.OpeningCash,
            ExpectedCash = request.OpeningCash,
            ActualCash = 0,
            CashDifference = 0,
            Status = PosSessionStatus.Open,
            Notes = request.Notes
        };

        _sessions.Add(session);

        if (request.OpeningCash > 0)
        {
            var openFloat = new CashDrawerTransaction
            {
                SessionId = session.Id,
                Type = CashMovementType.FloatIn,
                Amount = request.OpeningCash,
                Reason = "Opening cash float",
                PerformedByUserId = request.CashierUserId,
                Timestamp = session.OpenedAt
            };
            _drawerTransactions.Add(openFloat);
            session.DrawerTransactions.Add(openFloat);
        }

        await _sessions.SaveChangesAsync(cancellationToken);
        await _drawerTransactions.SaveChangesAsync(cancellationToken);

        return Map(session);
    }

    public async Task<PosSessionDto> Handle(CloseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetForUpdateAsync(request.SessionId, cancellationToken);

        if (session.Status == PosSessionStatus.Closed)
        {
            throw new ConflictException("Session is already closed.");
        }

        session.Status = PosSessionStatus.Closed;
        session.ClosedByUserId = request.ClosedByUserId;
        session.ClosedAt = DateTimeOffset.UtcNow;
        session.ActualCash = request.ActualCash;
        session.CashDifference = session.ActualCash - session.ExpectedCash;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                ? request.Notes
                : $"{session.Notes} | {request.Notes}";
        }

        await _sessions.SaveChangesAsync(cancellationToken);

        var transactions = await _drawerTransactions.Query(new Specification<CashDrawerTransaction>(
            t => t.SessionId == session.Id))
            .ToListAsync(cancellationToken);
        session.DrawerTransactions = transactions;

        return Map(session);
    }

    public async Task<CashDrawerTransactionDto> Handle(RecordDrawerTransactionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetForUpdateAsync(request.SessionId, cancellationToken);

        if (session.Status != PosSessionStatus.Open)
        {
            throw new ConflictException("Cannot record drawer transactions on a closed session.");
        }

        var tx = new CashDrawerTransaction
        {
            SessionId = session.Id,
            Type = request.Type,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            PerformedByUserId = request.PerformedByUserId,
            Timestamp = DateTimeOffset.UtcNow
        };

        _drawerTransactions.Add(tx);

        switch (request.Type)
        {
            case CashMovementType.FloatIn:
            case CashMovementType.PayIn:
                session.TotalPayIn += request.Amount;
                session.ExpectedCash += request.Amount;
                break;
            case CashMovementType.PayOut:
            case CashMovementType.DropOut:
                session.TotalPayOut += request.Amount;
                session.ExpectedCash -= request.Amount;
                break;
        }

        await _drawerTransactions.SaveChangesAsync(cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);

        return new CashDrawerTransactionDto(
            tx.Id,
            tx.SessionId,
            tx.Type,
            tx.Amount,
            tx.Reason,
            tx.PerformedByUserId,
            tx.Timestamp);
    }

    public async Task<PosSessionDto?> Handle(GetCurrentSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessions.Query(new Specification<PosSession>(
            s => s.RegisterId == request.RegisterId && s.Status == PosSessionStatus.Open))
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null) return null;

        var transactions = await _drawerTransactions.Query(new Specification<CashDrawerTransaction>(
            t => t.SessionId == session.Id))
            .OrderBy(t => t.Timestamp)
            .ToListAsync(cancellationToken);

        session.DrawerTransactions = transactions;

        return Map(session);
    }

    private static PosSessionDto Map(PosSession s) => new(
        s.Id,
        s.LegalEntityId,
        s.RegisterId,
        s.RegisterName,
        s.OpenedByUserId,
        s.CashierName,
        s.OpenedAt,
        s.ClosedByUserId,
        s.ClosedAt,
        s.OpeningCash,
        s.TotalSales,
        s.TotalReturns,
        s.TotalPayIn,
        s.TotalPayOut,
        s.ExpectedCash,
        s.ActualCash,
        s.CashDifference,
        s.Status,
        s.Notes,
        s.CustomFields,
        s.DrawerTransactions.Select(t => new CashDrawerTransactionDto(
            t.Id,
            t.SessionId,
            t.Type,
            t.Amount,
            t.Reason,
            t.PerformedByUserId,
            t.Timestamp)).ToList(),
        s.CreatedAt);
}
