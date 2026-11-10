using ERP.Application.Common;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class ResolveInteractionHandler(ICrmInteractionRepository interactions) : ICommandHandler<ResolveInteractionCommand, bool>
{
    public async Task<bool> Handle(ResolveInteractionCommand request, CancellationToken cancellationToken)
    {
        var interaction = await interactions.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Interaction {request.Id} not found.");

        interaction.IsResolved = true;
        await interactions.SaveChangesAsync(cancellationToken);
        return true;
    }
}
