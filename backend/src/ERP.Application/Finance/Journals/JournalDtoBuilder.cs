using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Builds the journal answer: its lines in order, with each account's code and name.</summary>
public sealed class JournalDtoBuilder(IAccountRepository accounts)
{
    public async Task<JournalDto> ToDtoAsync(JournalEntry j, CancellationToken ct)
    {
        var ids = j.Lines.Select(l => l.AccountId).Distinct().ToList();
        var names = (await accounts.ListLabelsAsync(ids, ct)).ToDictionary(a => a.Id);
        var lines = j.Lines.OrderBy(l => l.Seq).Select(l => new JournalLineDto(l.Id, l.Seq, l.AccountId, names[l.AccountId].Code, names[l.AccountId].Name, l.Debit, l.Credit, l.Description, l.PartyId)).ToList();
        return new JournalDto(j.Id, j.Number, j.LegalEntityId, j.Date, j.Memo, j.Reference, j.SourceType, j.SourceId, j.CurrencyCode, j.Status, j.Amount, j.ReversalOfId, j.PostedAt, lines, j.ApprovalRequestId);
    }
}
