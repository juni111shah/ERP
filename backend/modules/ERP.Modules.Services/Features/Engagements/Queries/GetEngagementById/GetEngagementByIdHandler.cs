using ERP.Application.Common;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class GetEngagementByIdHandler(IServiceEngagementRepository engagements) : IQueryHandler<GetEngagementByIdQuery, EngagementDto?>
{
    public async Task<EngagementDto?> Handle(GetEngagementByIdQuery request, CancellationToken cancellationToken)
    {
        var engagement = await engagements.FindAsync(request.Id, cancellationToken);
        return engagement is null ? null : EngagementDto.From(engagement);
    }
}
