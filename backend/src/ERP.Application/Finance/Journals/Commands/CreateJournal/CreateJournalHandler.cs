using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreateJournalHandler(ILegalEntityGuard guard, IJournalEntryRepository journals, JournalDrafts drafts, JournalDtoBuilder dtos)
    : ICommandHandler<CreateJournalCommand, JournalDto>
{
    public async Task<JournalDto> Handle(CreateJournalCommand command, CancellationToken ct)
    {
        // 1. A draft in the legal entity's base currency.
        var entity = await guard.RequireAsync(command.Input.LegalEntityId, ct);
        var journal = new JournalEntry { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency, SourceType = "manual" };
        journal.Number = "DRAFT-" + journal.Id.ToString("N")[^8..]; // the real number is allocated when posted, so numbers have no gaps

        // 2. Its lines, after checking accounts, parties and business units.
        journal.Lines.AddRange(await drafts.ApplyAsync(journal, command.Input, ct));

        // 3. Save.
        journals.Add(journal);
        await journals.SaveChangesAsync(ct);
        return await dtos.ToDtoAsync(journal, ct);
    }
}
