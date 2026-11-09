using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;

namespace ERP.Application.Platform;

internal sealed class SetClientDemoHandler(IRepository<Organization> organizations) : ICommandHandler<SetClientDemoCommand, bool>
{
    public async Task<bool> Handle(SetClientDemoCommand command, CancellationToken ct)
    {
        var org = await organizations.GetForUpdateAsync(command.OrgId, ct);
        org.IsDemo = command.IsDemo;
        await organizations.SaveChangesAsync(ct);
        return org.IsDemo;
    }
}
