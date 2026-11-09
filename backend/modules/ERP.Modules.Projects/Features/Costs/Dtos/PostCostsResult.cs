namespace ERP.Modules.Projects.Features;

/// <summary>One journal of a cost posting: where the month's cost went.</summary>
public sealed record PostedCostJournalDto(Guid JournalId, string JournalNumber, DateOnly Date, decimal ToWorkInProgress, decimal ToExpense);

/// <summary>JournalId and JournalNumber name the first journal, for callers that expect one; Journals lists them all.</summary>
public sealed record PostCostsResult(Guid ProjectId, int Entries, decimal Amount, Guid? JournalId, string? JournalNumber,
    IReadOnlyList<PostedCostJournalDto>? Journals = null, decimal ToWorkInProgress = 0m, decimal ToExpense = 0m);
