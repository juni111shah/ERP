using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

/// <summary>Rules shared by the commands that change a manual draft journal: loading it, releasing its approval, checking its accounts and building its lines.</summary>
public sealed class JournalDrafts(
    IJournalEntryRepository journals, IAccountRepository accounts, IReferenceCheck references, IApprovalService approvals, IReadRepository<ApprovalRequest> approvalRequests)
{
    /// <summary>The journal, tracked, when it is a manual draft; otherwise a conflict.</summary>
    public async Task<JournalEntry> LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var j = await journals.GetWithLinesForUpdateAsync(id, ct);
        if (j.Status != JournalStatus.Draft) throw new ConflictException("Only a draft journal can be changed. Reverse a posted one instead.");
        if (j.SourceType != "manual") throw new ConflictException("This entry was created by a document and cannot be edited here.");
        return j;
    }

    /// <summary>Withdraws the journal's approval request while it is still pending; a decided request only loses its link. Any change to a draft needs a fresh approval.</summary>
    public async Task ReleaseApprovalAsync(JournalEntry journal, CancellationToken ct)
    {
        if (journal.ApprovalRequestId is not { } rid) return;
        var request = await approvalRequests.FindAsync(rid, ct);
        if (request?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
        journal.ApprovalRequestId = null;
    }

    /// <summary>Manual journals bypass <see cref="IPostingService.PostAsync"/>, so they check the accounts themselves: found, active, not a header.</summary>
    public async Task EnsurePostableAsync(List<Guid> ids, CancellationToken ct)
    {
        var found = await accounts.ListPostingStatesAsync(ids, ct);
        if (found.Count != ids.Count) throw new FieldValidationException("lines", "One of the accounts was not found.");
        var bad = found.FirstOrDefault(a => !a.IsPostable || !a.IsActive);
        if (bad is not null) throw new FieldValidationException("lines", $"Account {bad.Code} cannot be posted to (inactive or a header account).");
    }

    /// <summary>Checks the input's accounts, parties and business units, applies the header fields and returns the new lines (the amount is their debit total).</summary>
    public async Task<List<JournalLine>> ApplyAsync(JournalEntry journal, JournalInput i, CancellationToken ct)
    {
        var ids = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        await EnsurePostableAsync(ids, ct);
        if (!await references.AllExistAsync<ERP.Domain.Parties.Party>(i.Lines.Where(l => l.PartyId.HasValue).Select(l => l.PartyId!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "A party was not found.");
        if (!await references.AllExistAsync<Domain.Organizations.BusinessUnit>(i.Lines.Where(l => l.BusinessUnitId.HasValue).Select(l => l.BusinessUnitId!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "A business unit was not found.");
        journal.Date = i.Date; journal.Memo = i.Memo?.Trim(); journal.Reference = i.Reference?.Trim();
        var seq = 1;
        var lines = i.Lines.Select(l => new JournalLine { JournalEntryId = journal.Id, Seq = seq++, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Description = l.Description, PartyId = l.PartyId, BusinessUnitId = l.BusinessUnitId }).ToList();
        journal.Amount = lines.Sum(l => l.Debit);
        return lines;
    }
}
