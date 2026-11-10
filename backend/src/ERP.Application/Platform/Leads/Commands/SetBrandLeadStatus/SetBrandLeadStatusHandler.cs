using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class SetBrandLeadStatusHandler(IBrandLeadRepository leads, IReadRepository<SignupRequest> signups, PlatformLog log) : ICommandHandler<SetBrandLeadStatusCommand, BrandLeadRow>
{
    public async Task<BrandLeadRow> Handle(SetBrandLeadStatusCommand command, CancellationToken ct)
    {
        var lead = await leads.GetForUpdateAsync(command.Id, ct);
        lead.Status = command.Status;
        log.Add($"Lead marked {command.Status.ToString().ToLowerInvariant()}", null, lead.Company, lead.Email);
        await leads.SaveChangesAsync(ct);
        return BrandLeadRow.From(lead, await signups.AnyAsync(s => s.Email == lead.Email, ct));
    }
}
