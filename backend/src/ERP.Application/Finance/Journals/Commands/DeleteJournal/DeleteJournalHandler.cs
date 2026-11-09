using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class DeleteJournalHandler(IJournalEntryRepository journals, JournalDrafts drafts) : ICommandHandler<DeleteJournalCommand>
{
    public async Task<Unit> Handle(DeleteJournalCommand command, CancellationToken ct)
    {
        // 1. Only a manual draft; its pending approval is withdrawn.
        var journal = await drafts.LoadDraftAsync(command.Id, ct);
        await drafts.ReleaseApprovalAsync(journal, ct);

        // 2. Delete.
        journals.Remove(journal);
        await journals.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
