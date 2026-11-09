using ERP.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateMyLeaveHandler(MyEmployee me, IServiceProvider services) : ICommandHandler<CreateMyLeaveCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(CreateMyLeaveCommand command, CancellationToken ct)
    {
        // 1. The employee comes from the link.
        var mine = await me.GetAsync(ct);
        var i = command.Input;

        // 2. The leave handler runs directly (not through the pipeline): the link is the authorisation.
        return await ActivatorUtilities.CreateInstance<CreateLeaveRequestHandler>(services)
            .Handle(new CreateLeaveRequestCommand(new LeaveRequestInput(mine.Id, i.LeaveTypeId, i.FromDate, i.ToDate, i.Reason, i.Submit)), ct);
    }
}
