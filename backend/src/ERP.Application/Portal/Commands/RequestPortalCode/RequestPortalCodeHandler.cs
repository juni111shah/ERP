using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Portal;

internal sealed class RequestPortalCodeHandler(IPortalAuth auth) : ICommandHandler<RequestPortalCodeCommand>
{
    public async Task<Unit> Handle(RequestPortalCodeCommand command, CancellationToken ct)
    {
        await auth.RequestCodeAsync(command.Workspace.Trim(), command.Email.Trim(), command.Kind, ct);
        return Unit.Value;
    }
}
