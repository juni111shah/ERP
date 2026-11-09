using ERP.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

internal sealed class CancelMyLeaveHandler(MyEmployee me, IServiceProvider services) : ICommandHandler<CancelMyLeaveCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(CancelMyLeaveCommand command, CancellationToken ct)
    {
        // 1. Only my own request.
        await me.RequireMineAsync(command.Id, ct);

        // 2. The leave handler runs directly (not through the pipeline): the link is the authorisation.
        return await ActivatorUtilities.CreateInstance<CancelLeaveRequestHandler>(services).Handle(new CancelLeaveRequestCommand(command.Id), ct);
    }
}
