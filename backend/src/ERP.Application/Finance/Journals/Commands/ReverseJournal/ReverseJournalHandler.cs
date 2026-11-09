using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class ReverseJournalHandler(IJournalEntryRepository journals, IPostingService posting, JournalDtoBuilder dtos)
    : ICommandHandler<ReverseJournalCommand, JournalDto>
{
    private static readonly HashSet<string> DocumentSourceTypes = new(StringComparer.OrdinalIgnoreCase) { "invoice", "creditnote", "bill", "receipt", "payment", "cheque" };
    private const string ManualSource = "manual";

    public async Task<JournalDto> Handle(ReverseJournalCommand command, CancellationToken ct)
    {
        // 1. Load it.
        var original = await journals.GetWithLinesForUpdateAsync(command.Id, ct);

        // 2. Journals owned by a document with its own void / cancel flow must be undone there, or the document and ledger drift apart.
        // Only journals entered by hand can be reversed here. Anything the system posted (documents, modules, automatic postings)
        // must be undone through its own flow, or the source record and the ledger drift apart.
        if (original.ReversalOfId is null && !string.Equals(original.SourceType, ManualSource, StringComparison.OrdinalIgnoreCase))
        {
            var src = original.SourceType;
            throw new ConflictException(DocumentSourceTypes.Contains(src ?? "")
                ? $"This entry was created by a source document ({src}). Void the document instead of reversing its journal."
                : $"This entry was posted automatically by the system ({src}). Only manual journals can be reversed; undo it from where it was created.");
        }

        // 3. Post the reversal and save.
        var reversal = command.Date is { } date
            ? await posting.ReverseAsync(original, date, command.Memo, ct)
            : await posting.ReverseTodayAsync(original, command.Memo, ct);
        await journals.SaveChangesAsync(ct);
        return await dtos.ToDtoAsync(reversal, ct);
    }
}
