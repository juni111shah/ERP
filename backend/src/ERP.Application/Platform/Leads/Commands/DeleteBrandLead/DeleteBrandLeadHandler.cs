using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Platform;

internal sealed class DeleteBrandLeadHandler(IBrandLeadRepository leads, PlatformLog log) : ICommandHandler<DeleteBrandLeadCommand>
{
    public async Task<Unit> Handle(DeleteBrandLeadCommand command, CancellationToken ct)
    {
        var lead = await leads.GetForUpdateAsync(command.Id, ct);
        leads.Remove(lead);
        log.Add("Lead deleted", null, lead.Company, null);
        await leads.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
