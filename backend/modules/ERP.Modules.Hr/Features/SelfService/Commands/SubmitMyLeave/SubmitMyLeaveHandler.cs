using ERP.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

internal sealed class SubmitMyLeaveHandler(MyEmployee me, IServiceProvider services) : ICommandHandler<SubmitMyLeaveCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(SubmitMyLeaveCommand command, CancellationToken ct)
    {
        // 1. Only my own request.
        await me.RequireMineAsync(command.Id, ct);

        // 2. The leave handler runs directly (not through the pipeline): the link is the authorisation.
        return await ActivatorUtilities.CreateInstance<SubmitLeaveRequestHandler>(services).Handle(new SubmitLeaveRequestCommand(command.Id), ct);
    }
}
