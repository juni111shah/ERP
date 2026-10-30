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
    decimal TotalReturns,
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

/// <summary>CashierUserId and CashierName are ignored: the opening cashier is the signed-in caller.</summary>
public record OpenSessionCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    decimal OpeningCash,
    Guid? CashierUserId,
    string? CashierName,
    string? Notes) : ICommand<PosSessionDto>;

/// <summary>ClosedByUserId is ignored: the closing user is the signed-in caller.</summary>
public record CloseSessionCommand(
    Guid SessionId,
    decimal ActualCash,
    Guid? ClosedByUserId,
    string? Notes) : ICommand<PosSessionDto>;

/// <summary>PerformedByUserId is ignored: the movement is attributed to the signed-in caller.</summary>
public record RecordDrawerTransactionCommand(
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    Guid? PerformedByUserId) : ICommand<CashDrawerTransactionDto>;

public record GetCurrentSessionQuery(Guid RegisterId) : IQuery<PosSessionDto?>;

public class OpenSessionValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.OpeningCash).GreaterThanOrEqualTo(0);
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
    /// <summary>Filtered unique index: one open session per register (see PosModule).</summary>
    public const string OneOpenSessionIndex = "UX_pos_Sessions_OneOpenPerRegister";

    private readonly IRepository<PosSession> _sessions;
    private readonly IRepository<PosRegister> _registers;
    private readonly IRepository<CashDrawerTransaction> _drawerTransactions;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IDbErrors _dbErrors;

    public SessionHandlers(
        IRepository<PosSession> sessions,
        IRepository<PosRegister> registers,
        IRepository<CashDrawerTransaction> drawerTransactions,
        ICurrentUser user,
        IUnitOfWork uow,
        IDbErrors dbErrors)
    {
        _sessions = sessions;
        _registers = registers;
        _drawerTransactions = drawerTransactions;
        _user = user;
        _uow = uow;
        _dbErrors = dbErrors;
    }

    private Guid CallerId => _user.UserId is { } id && id != Guid.Empty ? id : throw new ForbiddenException();

    private string CallerName => !string.IsNullOrWhiteSpace(_user.Email) ? _user.Email! : CallerId.ToString();

    public async Task<PosSessionDto> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        var caller = CallerId;
        var register = await _registers.GetAsync(request.RegisterId, cancellationToken);
        var conflict = $"Register '{register.Name}' already has an active open session.";

        var activeSession = await _sessions.Query(new Specification<PosSession>(
            s => s.RegisterId == request.RegisterId && s.Status == PosSessionStatus.Open))
            .FirstOrDefaultAsync(cancellationToken);
        if (activeSession != null) throw new ConflictException(conflict);

        try
        {
            return await _uow.InTransactionAsync(async token =>
            {
                var session = new PosSession
                {
                    LegalEntityId = request.LegalEntityId,
                    RegisterId = request.RegisterId,
                    RegisterName = register.Name,
                    OpenedByUserId = caller,
                    CashierName = CallerName,
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
                        PerformedByUserId = caller,
                        Timestamp = session.OpenedAt
                    };
                    _drawerTransactions.Add(openFloat);
                    session.DrawerTransactions.Add(openFloat);
                }

                await _sessions.SaveChangesAsync(token); // session and float in one save
                return Map(session);
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsOneOpenViolation(ex))
        {
            throw new ConflictException(conflict);
        }
    }

    private bool IsOneOpenViolation(Exception ex)
        => _dbErrors.IsUniqueViolation(ex, OneOpenSessionIndex) || ex.ToString().Contains("pos_Sessions.RegisterId", StringComparison.Ordinal); // SQLite names columns, not indexes

    public Task<PosSessionDto> Handle(CloseSessionCommand request, CancellationToken cancellationToken) => _uow.InTransactionAsync(async token =>
    {
        var caller = CallerId;
        var existing = await _sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId)).Select(s => new { s.Notes }).FirstOrDefaultAsync(token)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? existing.Notes
            : string.IsNullOrWhiteSpace(existing.Notes) ? request.Notes : $"{existing.Notes} | {request.Notes}";
        var closedAt = DateTimeOffset.UtcNow;
        var actual = request.ActualCash;

        // Close only while open, and take the difference from the committed ExpectedCash in the same statement.
        var rows = await _sessions.Tracked(new Specification<PosSession>(s => s.Id == request.SessionId && s.Status == PosSessionStatus.Open))
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Status, PosSessionStatus.Closed)
                .SetProperty(s => s.ClosedByUserId, (Guid?)caller)
                .SetProperty(s => s.ClosedAt, (DateTimeOffset?)closedAt)
                .SetProperty(s => s.ActualCash, actual)
                .SetProperty(s => s.CashDifference, s => actual - s.ExpectedCash)
                .SetProperty(s => s.Notes, notes), token);
        if (rows == 0) throw new ConflictException("Session is already closed.");

        var session = await _sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId)).FirstAsync(token);
        session.DrawerTransactions = await _drawerTransactions.Query(new Specification<CashDrawerTransaction>(t => t.SessionId == session.Id)).ToListAsync(token);
        return Map(session);
    }, cancellationToken);

    public Task<CashDrawerTransactionDto> Handle(RecordDrawerTransactionCommand request, CancellationToken cancellationToken) => _uow.InTransactionAsync(async token =>
    {
        var caller = CallerId;
        var amount = request.Amount;
        var openOnly = new Specification<PosSession>(s => s.Id == request.SessionId && s.Status == PosSessionStatus.Open);

        var rows = request.Type switch
        {
            CashMovementType.FloatIn or CashMovementType.PayIn => await _sessions.Tracked(openOnly).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.TotalPayIn, s => s.TotalPayIn + amount)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash + amount), token),
            CashMovementType.PayOut or CashMovementType.DropOut => await _sessions.Tracked(openOnly).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.TotalPayOut, s => s.TotalPayOut + amount)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - amount), token),
            _ => 0,
        };
        if (rows == 0)
        {
            if (!await _sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId)).AnyAsync(token))
                throw new NotFoundException($"Session {request.SessionId} not found");
            throw new ConflictException("Cannot record drawer transactions on a closed session.");
        }

        var tx = new CashDrawerTransaction
        {
            SessionId = request.SessionId,
            Type = request.Type,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            PerformedByUserId = caller,
            Timestamp = DateTimeOffset.UtcNow
        };
        _drawerTransactions.Add(tx);
        await _drawerTransactions.SaveChangesAsync(token);

        return new CashDrawerTransactionDto(tx.Id, tx.SessionId, tx.Type, tx.Amount, tx.Reason, tx.PerformedByUserId, tx.Timestamp);
    }, cancellationToken);

    public async Task<PosSessionDto?> Handle(GetCurrentSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessions.Query(new Specification<PosSession>(
            s => s.RegisterId == request.RegisterId && s.Status == PosSessionStatus.Open))
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null) return null;

        var transactions = (await _drawerTransactions.Query(new Specification<CashDrawerTransaction>(
            t => t.SessionId == session.Id))
            .ToListAsync(cancellationToken))
            .OrderBy(t => t.Timestamp)
            .ToList();

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
