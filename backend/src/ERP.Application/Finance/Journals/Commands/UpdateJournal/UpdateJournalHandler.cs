using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class UpdateJournalHandler(IJournalEntryRepository journals, IRepository<JournalLine> lineRows, JournalDrafts drafts, JournalDtoBuilder dtos)
    : ICommandHandler<UpdateJournalCommand, JournalDto>
{
    public async Task<JournalDto> Handle(UpdateJournalCommand command, CancellationToken ct)
    {
        // 1. Only a manual draft of the same legal entity; a change needs a fresh approval.
        var journal = await drafts.LoadDraftAsync(command.Id, ct);
        if (command.Input.LegalEntityId != journal.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a journal cannot change.");
        await drafts.ReleaseApprovalAsync(journal, ct);

        // 2. Replace the lines. New children are added through their own repository: a client-side GUID added through the
        // parent's navigation alone is treated as an existing row (concurrency error).
        foreach (var old in journal.Lines.ToList()) lineRows.Remove(old);
        var fresh = await drafts.ApplyAsync(journal, command.Input, ct);
        lineRows.AddRange(fresh);
        await journals.SaveChangesAsync(ct);

        // 3. Answer with the new lines.
        journal.Lines.Clear();
        journal.Lines.AddRange(fresh);
        return await dtos.ToDtoAsync(journal, ct);
    }
}
