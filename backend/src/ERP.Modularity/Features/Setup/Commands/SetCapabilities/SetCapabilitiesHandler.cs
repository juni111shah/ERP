using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Templates;
using MediatR;

namespace ERP.Modularity.Features;

internal sealed class SetCapabilitiesHandler(
    ISettingsService settings, IUnitOfWork unitOfWork, IRequestHandler<GetBusinessProfileQuery, BusinessProfile> profile, ICurrentUser user, IAccessChangeNotifier accessChanges)
    : ICommandHandler<SetCapabilitiesCommand, BusinessProfile>
{
    public async Task<BusinessProfile> Handle(SetCapabilitiesCommand command, CancellationToken ct)
    {
        var unknown = command.Input.Disabled.FirstOrDefault(k => CoreCapabilities.Find(k) is null);
        if (unknown is not null) throw new FieldValidationException("disabled", $"Unknown area '{unknown}'.");
        await settings.SetRawAsync(CoreCapabilities.SettingKey, JsonSerializer.Serialize(command.Input.Disabled.Distinct()), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        // Hidden areas leave the menu of every member at once.
        if (user.OrgId is { } orgId) await accessChanges.WorkspaceChangedAsync(orgId, AccessChangeReasons.CapabilitiesChanged, ct);
        return await profile.Handle(new GetBusinessProfileQuery(), ct);
    }
}
