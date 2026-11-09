using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetTrialBalanceHandler(TrialBalanceBuilder trialBalance) : IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery q, CancellationToken ct)
    {
        var rows = await trialBalance.BuildAsync(q.From, q.To, q.LegalEntityId, ct);
        return new TrialBalanceDto(q.From, q.To, rows, rows.Sum(r => r.Debit), rows.Sum(r => r.Credit), rows.Sum(r => r.Closing) == 0);
    }
}
