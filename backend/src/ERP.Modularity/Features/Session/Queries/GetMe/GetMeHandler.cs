using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;

namespace ERP.Modularity.Features;

internal sealed class GetMeHandler(
    ICurrentUser user, IUserDirectory directory, IAuthService auth, IReadRepository<Organization> organizations, ILegalEntityRepository legalEntities, ModuleRegistry modules,
    ERP.Application.Platform.IPlatformDirectory platform, ERP.Application.Platform.ISubscriptionAccess subscription) : IQueryHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> Handle(GetMeQuery query, CancellationToken ct)
    {
        // 1. Who is asking, and in which workspace.
        var userId = user.UserId ?? throw new ForbiddenException();
        var profile = await directory.GetProfileAsync(userId, ct) ?? throw new NotFoundException();
        var org = await organizations.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);

        // 2. What they may do there and which legal entities they see.
        var (permissions, allowedEntities, enabled) = await auth.ResolveAccessAsync(userId, org.Id, ct);
        var entities = (await legalEntities.ListAllowedAsync(allowedEntities, ct)).Select(e => new MeLegalEntity(e.Id, e.Code, e.Name, e.BaseCurrency)).ToList();

        // 3. The answer.
        return new(
            new(profile.Id, profile.Email, profile.FullName, profile.MustChangePassword),
            new(org.Id, org.Code, org.Name, org.IndustryCode, org.CountryCode, org.BaseCurrency, org.TimeZone, org.DefaultLocale, org.IsDemo),
            await auth.WorkspacesForAsync(userId, ct),
            permissions.OrderBy(p => p).ToList(),
            entities,
            allowedEntities is null,
            modules.Modules.Where(m => !m.Manifest.Hidden && (m.Manifest.IsCore || enabled.Contains(m.Manifest.Key))).Select(m => new MeModule(m.Manifest.Key, m.Manifest.UiRoute)).ToList(),
            await platform.IsPlatformAdminAsync(userId, ct), await subscription.GetAsync(org.Id, ct), org.IsInternal);
    }
}
