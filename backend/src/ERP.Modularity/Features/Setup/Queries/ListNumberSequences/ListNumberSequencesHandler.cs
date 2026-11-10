using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modularity.Features;

internal sealed class ListNumberSequencesHandler(INumberSequenceRepository sequences, IClock clock) : IQueryHandler<ListNumberSequencesQuery, IReadOnlyList<NumberSequenceView>>
{
    public async Task<IReadOnlyList<NumberSequenceView>> Handle(ListNumberSequencesQuery query, CancellationToken ct)
    {
        var rows = await sequences.ListWorkspaceWideAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return rows.GroupBy(s => s.DocumentType).Select(g => g.OrderByDescending(s => s.Year).First()).OrderBy(s => s.DocumentType)
            .Select(s => NumberSequenceView.From(s, today)).ToList();
    }
}
