using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class JournalEntryRepository(IRepository<JournalEntry> journals) : RepositoryBase<JournalEntry>(journals), IJournalEntryRepository
{
    public async Task<JournalEntry> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<JournalEntry>(x => x.Id == id).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<JournalEntry> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<JournalEntry>(x => x.Id == id).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<JournalEntry?> FindOpeningEntryForUpdateAsync(Guid journalId, CancellationToken ct)
        => Tracked(new Specification<JournalEntry>(j => j.Id == journalId && j.SourceType == LedgerSources.Opening && j.ReversalOfId == null).Include(j => j.Lines)).FirstOrDefaultAsync(ct);

    public Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct)
        => Query().SelectMany(j => j.Lines).AnyAsync(l => l.AccountId == accountId, ct);

    public Task<List<LedgerMovement>> ListPostedMovementsAsync(DateOnly to, Guid? entityId, bool excludeYearEnd, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entityId == null || j.LegalEntityId == entityId) && (!excludeYearEnd || j.SourceType != LedgerSources.YearEnd)))
            .SelectMany(j => j.Lines, (j, l) => new LedgerMovement(l.AccountId, j.Date, l.Debit, l.Credit))
            .ToListAsync(ct);

    public Task<List<AccountLedgerLine>> ListPostedLinesOfAccountAsync(Guid accountId, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId), (j, l) => new AccountLedgerLine(j.Id, j.Number, j.Date, j.Memo, l.Description, l.Debit, l.Credit, l.Seq))
            .ToListAsync(ct);

    public Task<List<LineAmounts>> ListPostedAmountsBeforeAsync(Guid entityId, DateOnly before, List<Guid> accountIds, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == entityId && j.Date < before))
            .SelectMany(j => j.Lines.Where(l => accountIds.Contains(l.AccountId)), (j, l) => new LineAmounts(l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<AccountMovement>> ListPostedMovementsOfAccountsAsync(Guid entityId, DateOnly to, List<Guid> accountIds, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == entityId && j.Date <= to))
            .SelectMany(j => j.Lines.Where(l => accountIds.Contains(l.AccountId)), (j, l) => new AccountMovement(l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<AccountMovement>> ListPostedMovementsExcludingYearEndAsync(Guid entityId, DateOnly from, DateOnly to, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.SourceType != LedgerSources.YearEnd && j.LegalEntityId == entityId && j.Date >= from && j.Date <= to))
            .SelectMany(j => j.Lines, (j, l) => new AccountMovement(l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<AccountMovement>> ListPostedMovementsAsync(IReadOnlyCollection<Guid>? accountIds, Guid? entityId, CancellationToken ct)
    {
        var query = Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines, (j, l) => new { l.AccountId, l.Debit, l.Credit });
        if (accountIds is not null) query = query.Where(l => accountIds.Contains(l.AccountId));
        return query.Select(l => new AccountMovement(l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);
    }

    public Task<List<CashLine>> ListPostedCashLinesAsync(List<Guid> cashIds, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => cashIds.Contains(l.AccountId)), (j, l) => new CashLine(j.Date, j.SourceType, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<JournalLineAmounts>> ListLinesOfCashJournalsAsync(List<Guid> cashIds, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.SourceType != LedgerSources.Opening && j.Date >= from && j.Date <= to
                && (entityId == null || j.LegalEntityId == entityId) && j.Lines.Any(l => cashIds.Contains(l.AccountId))))
            .SelectMany(j => j.Lines, (j, l) => new JournalLineAmounts(j.Id, l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<CashAccountLine>> ListPostedCashAccountLinesAsync(List<Guid> accountIds, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => accountIds.Contains(l.AccountId)), (j, l) => new CashAccountLine(l.AccountId, j.Date, j.SourceType, l.Debit, l.Credit)).ToListAsync(ct);
    public async Task<JournalEntry> GetWithLinesForUpdateAsync(Guid? id, CancellationToken ct)
        => await Tracked(new Specification<JournalEntry>(j => j.Id == id).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<BankLedgerLine>> ListAccountLinesAsync(Guid accountId, Guid? entityId, DateOnly asOf, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= asOf && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId), (j, l) => new BankLedgerLine(l.Id, j.Id, j.Number, j.Date, l.Description ?? j.Memo, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<AccountMovement>> ListMovementsAsync(List<Guid> accountIds, DateOnly asOf, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= asOf && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => accountIds.Contains(l.AccountId)), (j, l) => new AccountMovement(l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);
}
