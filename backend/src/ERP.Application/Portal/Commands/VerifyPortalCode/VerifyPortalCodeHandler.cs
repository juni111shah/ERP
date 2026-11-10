using ERP.Application.Common;

namespace ERP.Application.Portal;

internal sealed class VerifyPortalCodeHandler(IPortalAuth auth) : ICommandHandler<VerifyPortalCodeCommand, PortalSession>
{
    public Task<PortalSession> Handle(VerifyPortalCodeCommand command, CancellationToken ct)
        => auth.VerifyCodeAsync(command.Workspace.Trim(), command.Email.Trim(), command.Code.Trim(), command.Kind, ct);
}
