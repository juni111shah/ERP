using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

internal sealed class OpenSessionHandler(
    IPosSessionRepository sessions,
    IRepository<PosRegister> registers,
    IRepository<CashDrawerTransaction> drawerTransactions,
    PosSessionCaller caller,
    IUnitOfWork uow,
    IDbErrors dbErrors) : ICommandHandler<OpenSessionCommand, PosSessionDto>
{
    /// <summary>Filtered unique index: one open session per register (see PosModule).</summary>
    public const string OneOpenSessionIndex = "UX_pos_Sessions_OneOpenPerRegister";

    public async Task<PosSessionDto> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        // 1. The caller opens the shift, on an active register of the legal entity that has no open shift.
        var callerId = caller.Id;
        var callerName = await caller.NameAsync(cancellationToken);
        var register = await registers.GetAsync(request.RegisterId, cancellationToken);
        if (!register.IsActive) throw new ConflictException($"Register '{register.Name}' is inactive.");
        if (register.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("legalEntityId", "The register belongs to another legal entity.");
        var conflict = $"Register '{register.Name}' already has an active open session.";

        var activeSession = await sessions.FindOpenForRegisterAsync(request.RegisterId, cancellationToken);
        if (activeSession != null) throw new ConflictException(conflict);

        // 2. Save the shift and its opening float together; a second till opening at the same moment hits the unique index.
        try
        {
            return await uow.InTransactionAsync(async token =>
            {
                var session = new PosSession
                {
                    LegalEntityId = request.LegalEntityId,
                    RegisterId = request.RegisterId,
                    RegisterName = register.Name,
                    OpenedByUserId = callerId,
                    CashierName = callerName,
                    OpenedAt = DateTimeOffset.UtcNow,
                    OpeningCash = request.OpeningCash,
                    ExpectedCash = request.OpeningCash,
                    ActualCash = 0,
                    CashDifference = 0,
                    Status = PosSessionStatus.Open,
                    Notes = request.Notes
                };
                sessions.Add(session);

                if (request.OpeningCash > 0)
                {
                    var openFloat = new CashDrawerTransaction
                    {
                        SessionId = session.Id,
                        Type = CashMovementType.FloatIn,
                        Amount = request.OpeningCash,
                        Reason = "Opening cash float",
                        PerformedByUserId = callerId,
                        PerformedByName = callerName,
                        Timestamp = session.OpenedAt
                    };
                    drawerTransactions.Add(openFloat);
                    session.DrawerTransactions.Add(openFloat);
                }

                await sessions.SaveChangesAsync(token); // session and float in one save
                return PosSessionDto.From(session);
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsOneOpenViolation(ex))
        {
            throw new ConflictException(conflict);
        }
    }

    private bool IsOneOpenViolation(Exception ex)
        => dbErrors.IsUniqueViolation(ex, OneOpenSessionIndex) || ex.ToString().Contains("pos_Sessions.RegisterId", StringComparison.Ordinal); // SQLite names columns, not indexes
}
