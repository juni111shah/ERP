using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>One bank statement line as the client imports it.</summary>
public sealed record StatementLineInput(DateOnly Date, string Description, string? Reference, decimal Amount);

/// <summary>How many lines were imported and how many were skipped as already imported.</summary>
public sealed record ImportResult(int Imported, int Duplicates);

/// <summary>An imported bank statement line and what it is matched to.</summary>
public sealed record StatementLineDto(Guid Id, Guid AccountId, DateOnly Date, string Description, string? Reference, decimal Amount, StatementLineStatus Status, Guid? MatchedJournalLineId)
{
    public static StatementLineDto From(BankStatementLine l) => new(l.Id, l.AccountId, l.Date, l.Description, l.Reference, l.Amount, l.Status, l.MatchedJournalLineId);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<BankStatementLine, StatementLineDto>> Projection =
        l => new StatementLineDto(l.Id, l.AccountId, l.Date, l.Description, l.Reference, l.Amount, l.Status, l.MatchedJournalLineId);
}

/// <summary>What identifies an imported statement line when the same file is imported again.</summary>
public sealed record StatementLineKey(DateOnly Date, decimal Amount, string? Reference, string Description);
