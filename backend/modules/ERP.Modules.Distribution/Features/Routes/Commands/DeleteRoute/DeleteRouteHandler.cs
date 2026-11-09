using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;
using MediatR;

namespace ERP.Modules.Distribution.Features;

internal sealed class DeleteRouteHandler(IDistributionRouteRepository routes) : ICommandHandler<DeleteRouteCommand>
{
    public async Task<Unit> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await routes.GetForUpdateAsync(request.Id, cancellationToken);
        routes.Remove(route);
        await routes.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
