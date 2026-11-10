using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class LockBooksHandler(ILegalEntityGuard guard, IFiscalYearCloseRepository closes, IRepository<LegalEntity> entities, IClock clock)
    : ICommandHandler<LockBooksCommand>
{
    public async Task<Unit> Handle(LockBooksCommand command, CancellationToken ct)
    {
        // 1. A legal entity of the caller, and never a future date.
        await guard.RequireAsync(command.LegalEntityId, ct);
        if (command.Through is { } d && d > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("through", "Books cannot be locked to a future date.");

        // 2. A closed year stays locked: posting into it would leave income and expense balances behind the closing entry.
        var closedTo = await closes.GetLatestClosedYearEndAsync(command.LegalEntityId, ct);
        if (closedTo is { } closed && (command.Through is null || command.Through < closed))
            throw new ConflictException($"Fiscal years are closed up to {closed:yyyy-MM-dd}. Reopen the year to move the lock before that date.");

        // 3. Move the lock and save.
        var entity = await entities.GetForUpdateAsync(command.LegalEntityId, ct);
        entity.BooksLockedTo = command.Through;
        await entities.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
