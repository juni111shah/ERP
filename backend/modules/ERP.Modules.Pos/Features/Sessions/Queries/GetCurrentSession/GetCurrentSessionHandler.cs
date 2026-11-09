using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetCurrentSessionHandler(IPosSessionRepository sessions, ICashDrawerTransactionRepository drawerTransactions)
    : IQueryHandler<GetCurrentSessionQuery, PosSessionDto?>
{
    public async Task<PosSessionDto?> Handle(GetCurrentSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindOpenForRegisterAsync(request.RegisterId, cancellationToken);

        if (session == null) return null;

        session.DrawerTransactions = (await drawerTransactions.ListForSessionAsync(session.Id, cancellationToken))
            .OrderBy(t => t.Timestamp)
            .ToList();

        return PosSessionDto.From(session);
    }
}
