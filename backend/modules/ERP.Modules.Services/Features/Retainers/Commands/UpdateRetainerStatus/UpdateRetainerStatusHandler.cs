using ERP.Application.Common;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class UpdateRetainerStatusHandler(IRetainerContractRepository retainers) : ICommandHandler<UpdateRetainerStatusCommand, RetainerDto>
{
    public async Task<RetainerDto> Handle(UpdateRetainerStatusCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and check the move is allowed.
        var contract = await retainers.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Retainer contract {request.Id} not found.");

        ServicesTransitions.Retainer.Ensure(contract.Status, request.Status, isSystem: false);

        // 2. Move it and save.
        contract.Status = request.Status;
        await retainers.SaveChangesAsync(cancellationToken);
        return RetainerDto.From(contract);
    }
}
