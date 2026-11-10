using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetCashMovementsHandler(CashMovementsCalculator movements) : IQueryHandler<GetCashMovementsQuery, CashMovementsDto>
{
    public Task<CashMovementsDto> Handle(GetCashMovementsQuery q, CancellationToken ct) => movements.ComputeAsync(q.From, q.To, q.LegalEntityId, ct);
}
