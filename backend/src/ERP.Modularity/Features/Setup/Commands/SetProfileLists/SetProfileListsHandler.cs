using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Templates;
using MediatR;

namespace ERP.Modularity.Features;

internal sealed class SetProfileListsHandler(ISettingsService settings, IUnitOfWork unitOfWork, IRequestHandler<GetBusinessProfileQuery, BusinessProfile> profile)
    : ICommandHandler<SetProfileListsCommand, BusinessProfile>
{
    public async Task<BusinessProfile> Handle(SetProfileListsCommand command, CancellationToken ct)
    {
        if (command.Input.Units is { } units) await settings.SetRawAsync(ModuleProvisioning.UnitsKey, JsonSerializer.Serialize(Clean(units)), ct: ct);
        if (command.Input.ItemCategories is { } categories) await settings.SetRawAsync(ModuleProvisioning.ItemCategoriesKey, JsonSerializer.Serialize(Clean(categories)), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await profile.Handle(new GetBusinessProfileQuery(), ct);
    }

    private static IEnumerable<string> Clean(IEnumerable<string> values) => values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
}
