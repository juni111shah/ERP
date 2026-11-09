using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class UnmatchStatementLineHandler(IBankStatementLineRepository lines) : ICommandHandler<UnmatchStatementLineCommand, StatementLineDto>
{
    public async Task<StatementLineDto> Handle(UnmatchStatementLineCommand command, CancellationToken ct)
    {
        var l = await lines.GetForUpdateAsync(command.LineId, ct);
        l.MatchedJournalLineId = null; l.Status = StatementLineStatus.Unmatched;
        await lines.SaveChangesAsync(ct);
        return StatementLineDto.From(l);
    }
}
