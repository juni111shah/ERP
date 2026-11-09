using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record ActivityRow(Guid Id, DateTime At, string Actor, string Action, Guid? OrgId, string? ClientName, string? Detail)
{
    internal static ActivityRow From(PlatformActivity a) => new(a.Id, a.At, a.Actor, a.Action, a.OrgId, a.ClientName, a.Detail);
}
