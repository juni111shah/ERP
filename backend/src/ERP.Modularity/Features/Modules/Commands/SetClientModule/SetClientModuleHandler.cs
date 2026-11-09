using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Modularity.Features;

/// <summary>
/// Runs the workspace's own module switch inside the client's workspace. It calls that handler directly, not through the mediator,
/// because the platform admin holds no permission in the client's workspace.
/// </summary>
internal sealed class SetClientModuleHandler(IOrgContext org, IRequestHandler<SetModuleStateCommand, ModuleState> setModuleState) : ICommandHandler<SetClientModuleCommand, ModuleState>
{
    public async Task<ModuleState> Handle(SetClientModuleCommand command, CancellationToken ct)
    {
        using (org.Use(command.OrgId)) return await setModuleState.Handle(new SetModuleStateCommand(command.Key, command.Enable), ct);
    }
}
