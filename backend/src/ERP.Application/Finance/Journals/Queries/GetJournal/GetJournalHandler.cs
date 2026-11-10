using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetJournalHandler(IJournalEntryRepository journals, JournalDtoBuilder dtos) : IQueryHandler<GetJournalQuery, JournalDto>
{
    public async Task<JournalDto> Handle(GetJournalQuery query, CancellationToken ct)
        => await dtos.ToDtoAsync(await journals.GetWithLinesAsync(query.Id, ct), ct);
}
