using ERP.Modularity;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Assets;

internal static class AssetsEndpoints
{
    private const string Base = "/api/v1/m/assets";

    public static void Map(RouteGroupBuilder group)
    {
        var categories = group.MapGroup("/categories");
        categories.MapPagedList<AssetCategory, AssetCategoryDto>("", Permissions.CategoriesRead,
            c => new AssetCategoryDto(c.Id, c.Code, c.Name, c.LifeMonths, c.Method, c.DecliningRatePercent, c.AssetAccountId, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId, c.DisposalGainLossAccountId, c.IsActive));
        categories.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAssetCategory(id), ct)).RequirePermission(Permissions.CategoriesRead);
        categories.MapPost("", async (AssetCategoryInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateAssetCategory(input), ct);
            return Results.Created($"{Base}/categories/{created.Id}", created);
        }).RequirePermission(Permissions.CategoriesManage);
        categories.MapPut("/{id:guid}", (Guid id, AssetCategoryInput input, ISender s, CancellationToken ct) => s.Send(new UpdateAssetCategory(id, input), ct)).RequirePermission(Permissions.CategoriesManage);
        categories.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteAssetCategory(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.CategoriesManage);

        var register = group.MapGroup("/register");
        register.MapPagedList<FixedAsset, AssetDto>("", Permissions.RegisterRead,
            a => new AssetDto(a.Id, a.LegalEntityId, a.Number, a.Name, a.Description, a.CategoryId, a.AcquisitionDate, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.DecliningRatePercent, a.BusinessUnitId, a.Custodian, a.Status,
                a.AccumulatedDepreciation, a.CapitalisedOn, a.CapitalisationSource, a.CapitalisationJournalId, a.SourceBillId, a.SourceBillLineId, a.DisposedOn, a.DisposalProceeds, a.DisposalGainLoss, a.DisposalJournalId));
        register.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAsset(id), ct)).RequirePermission(Permissions.RegisterRead);
        register.MapPost("", async (AssetInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateAsset(input), ct);
            return Results.Created($"{Base}/register/{created.Id}", created);
        }).RequirePermission(Permissions.RegisterManage);
        register.MapPut("/{id:guid}", (Guid id, AssetInput input, ISender s, CancellationToken ct) => s.Send(new UpdateAsset(id, input), ct)).RequirePermission(Permissions.RegisterManage);
        register.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteAsset(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.RegisterManage);
        register.MapPost("/{id:guid}/capitalise", (Guid id, CapitaliseInput input, ISender s, CancellationToken ct) => s.Send(new CapitaliseAsset(id, input), ct)).RequirePermission(Permissions.RegisterCapitalise);
        register.MapPost("/{id:guid}/dispose", (Guid id, DisposeInput input, ISender s, CancellationToken ct) => s.Send(new DisposeAsset(id, input), ct)).RequirePermission(Permissions.RegisterDispose);
        register.MapGet("/{id:guid}/schedule", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAssetSchedule(id), ct)).RequirePermission(Permissions.ReportsRead);

        var runs = group.MapGroup("/depreciation-runs");
        runs.MapPagedList<DepreciationRun, DepreciationRunDto>("", Permissions.DepreciationRead,
            r => new DepreciationRunDto(r.Id, r.LegalEntityId, r.Year, r.Month, r.PeriodEnd, r.JournalEntryId, r.Total, r.AssetCount, r.RunAt, r.RunBy, null, true));
        runs.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetDepreciationRun(id), ct)).RequirePermission(Permissions.DepreciationRead);
        runs.MapPost("", (RunDepreciation cmd, ISender s, CancellationToken ct) => s.Send(cmd, ct)).RequirePermission(Permissions.DepreciationRun);

        var reports = group.MapGroup("/reports");
        reports.MapGet("/register", (DateOnly asOf, Guid? legalEntityId, Guid? categoryId, ISender s, CancellationToken ct) => s.Send(new GetAssetRegister(asOf, legalEntityId, categoryId), ct)).RequirePermission(Permissions.ReportsRead);
        reports.MapGet("/schedule", (DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetDepreciationSchedule(from, to, legalEntityId), ct)).RequirePermission(Permissions.ReportsRead);
    }
}
