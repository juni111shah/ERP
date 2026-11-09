using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Workflow;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

internal sealed class PostJournalHandler(
    IJournalEntryRepository journals, JournalDrafts drafts, JournalDtoBuilder dtos, ILegalEntityGuard guard, IApprovalService approvals,
    IReadRepository<ApprovalRequest> approvalRequests, ISettingsService settings, INumberingService numbering, ICurrentUser user, IClock clock, IUnitOfWork uow)
    : ICommandHandler<PostJournalCommand, JournalDto>
{
    private const string JournalApprovalSetting = "core.journalRequiresApproval";

    public Task<JournalDto> Handle(PostJournalCommand command, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(command, token), ct);

    private async Task<JournalDto> PostAsync(PostJournalCommand command, CancellationToken ct)
    {
        // 1. A balanced manual draft on postable accounts, in an open period.
        var journal = await drafts.LoadDraftAsync(command.Id, ct);
        var entity = await guard.RequireAsync(journal.LegalEntityId, ct);
        await drafts.EnsurePostableAsync(journal.Lines.Select(l => l.AccountId).Distinct().ToList(), ct); // an account may have been deactivated since the draft was saved
        journal.Recalculate();
        journal.EnsureCanPost(entity.BooksLockedTo);

        // 2. Maker-checker: a matching policy, or the workspace setting, sends the journal for approval. It stays a draft, so reports never see it.
        if (await approvals.RequiresApprovalAsync(ApprovalDocumentTypes.Journal, journal.Amount, ct) || await settings.GetAsync<bool>(JournalApprovalSetting, journal.LegalEntityId, ct))
        {
            var request = journal.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                var label = journal.Reference ?? journal.Memo ?? journal.Number;
                var submitted = await approvals.SubmitAsync(ApprovalDocumentTypes.Journal, journal.Id, $"Journal {(label.Length <= 200 ? label : label[..200])}", journal.Amount, null, journal.LegalEntityId, ct);
                journal.ApprovalRequestId = submitted.Id;
                await journals.SaveChangesAsync(ct);
                return await dtos.ToDtoAsync(journal, ct);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This journal is waiting for approval.");
        }

        // 3. Number, post and save.
        journal.Number = await numbering.NextAsync("journal", entity.Id, journal.Date, ct);
        journal.Post(user.UserId ?? Guid.Empty, clock.UtcNow, entity.BooksLockedTo);
        await journals.SaveChangesAsync(ct);
        return await dtos.ToDtoAsync(journal, ct);
    }
}
