using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

/// <summary>Checks and field mapping shared by the asset register commands.</summary>
internal sealed class FixedAssetChecks(IAssetCategoryRepository categories, IReadRepository<BusinessUnit> businessUnits)
{
    public async Task<AssetCategory> RequireCategoryAsync(Guid id, CancellationToken ct)
        => await categories.FindAsync(id, ct) ?? throw new FieldValidationException("categoryId", "Asset category not found.");

    public async Task RequireBusinessUnitAsync(Guid? id, Guid legalEntityId, CancellationToken ct)
    {
        if (id is { } unitId && !await businessUnits.AnyAsync(u => u.Id == unitId && u.LegalEntityId == legalEntityId, ct))
            throw new FieldValidationException("businessUnitId", "Business unit not found in this legal entity.");
    }

    /// <summary>Copies the input onto the asset; the life falls back to the asset's own (same category) or the category's.</summary>
    public static void Apply(FixedAsset a, AssetInput i, AssetCategory category)
    {
        a.Name = i.Name.Trim(); a.Description = i.Description?.Trim(); a.CategoryId = category.Id; a.AcquisitionDate = i.AcquisitionDate; a.Cost = i.Cost; a.SalvageValue = i.SalvageValue;
        a.LifeMonths = i.LifeMonths ?? (a.LifeMonths > 0 && a.CategoryId == category.Id ? a.LifeMonths : category.LifeMonths); a.BusinessUnitId = i.BusinessUnitId; a.Custodian = i.Custodian?.Trim();
    }
}
