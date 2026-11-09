using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class UpdateClientNotesHandler(ClientSubscriptions clients, PlatformLog log) : ICommandHandler<UpdateClientNotesCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(UpdateClientNotesCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        s.BillingEmail = command.Input.BillingEmail?.Trim();
        s.ContactPhone = command.Input.ContactPhone?.Trim();
        s.Notes = command.Input.Notes?.Trim();
        log.Add("Client details updated", command.OrgId, org.Name);
        return await clients.SavedAsync(command.OrgId, ct);
    }
}
