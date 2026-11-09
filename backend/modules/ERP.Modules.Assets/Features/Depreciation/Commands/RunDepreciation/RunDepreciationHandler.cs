using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class RunDepreciationHandler(
    IDepreciationRunRepository runs, IDepreciationEntryRepository entryRows, IFixedAssetRepository assets, IAssetCategoryRepository categories,
    ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow, ICurrentUser user, IClock clock) : ICommandHandler<RunDepreciationCommand, DepreciationRunDto>
{
    private const string Source = "fixedasset.depreciation";

    public Task<DepreciationRunDto> Handle(RunDepreciationCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => RunCoreAsync(cmd, token), ct);

    private async Task<DepreciationRunDto> RunCoreAsync(RunDepreciationCommand cmd, CancellationToken ct)
    {
        // 1. A month that already ran returns its first run.
        var entity = await guard.RequireAsync(cmd.LegalEntityId, ct);
        var existing = await runs.FindForPeriodAsync(entity.Id, cmd.Year, cmd.Month, ct);
        if (existing is not null) return DepreciationRunDto.From(existing, null, alreadyRun: true);

        // 2. The month must have ended, and runs only go forward.
        var periodEnd = DepreciationCalculator.EndOfMonth(cmd.Year, cmd.Month);
        if (periodEnd > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("month", $"{periodEnd:yyyy-MM} has not ended yet.");
        var later = await runs.LatestPeriodEndAfterAsync(entity.Id, periodEnd, ct);
        if (later is { } latest) throw new ConflictException($"Depreciation has already run for {latest:yyyy-MM}. Runs go forward: a skipped month was caught up then.");

        // 3. The capitalised assets and their categories.
        var due = await assets.ListDueForDepreciationForUpdateAsync(entity.Id, periodEnd, ct);
        var categoryIds = due.Select(a => a.CategoryId).Distinct().ToList();
        var byCategory = await categories.MapByIdsAsync(categoryIds, ct);

        // 4. Charge each asset what its schedule expects by the month-end, never below the salvage value.
        var run = new DepreciationRun { LegalEntityId = entity.Id, Year = cmd.Year, Month = cmd.Month, PeriodEnd = periodEnd, RunAt = clock.UtcNow, RunBy = user.UserId };
        var rows = new List<DepreciationEntry>();
        var charges = new List<(FixedAsset Asset, AssetCategory Category, decimal Amount)>();
        foreach (var asset in due.OrderBy(a => a.Number))
        {
            var depreciable = asset.Cost - asset.SalvageValue;
            var amount = Math.Min(DepreciationCalculator.AccumulatedThrough(DepreciationCalculator.PlanOf(asset), periodEnd), depreciable) - asset.AccumulatedDepreciation;
            if (amount <= 0) continue;
            asset.AccumulatedDepreciation += amount;
            if (asset.AccumulatedDepreciation >= depreciable) asset.Status = AssetStatus.FullyDepreciated;
            rows.Add(new DepreciationEntry { LegalEntityId = entity.Id, RunId = run.Id, AssetId = asset.Id, PeriodEnd = periodEnd, Amount = amount, AccumulatedAfter = asset.AccumulatedDepreciation });
            charges.Add((asset, byCategory[asset.CategoryId], amount));
        }

        // 5. One journal, grouped by account and business unit.
        if (charges.Count > 0)
        {
            var memo = $"Depreciation {periodEnd:yyyy-MM}";
            var lines = charges.GroupBy(c => (c.Category.DepreciationExpenseAccountId, c.Asset.BusinessUnitId)).Select(g => new PostingLine(g.Key.DepreciationExpenseAccountId, g.Sum(c => c.Amount), 0, memo, null, g.Key.BusinessUnitId))
                .Concat(charges.GroupBy(c => (c.Category.AccumulatedDepreciationAccountId, c.Asset.BusinessUnitId)).Select(g => new PostingLine(g.Key.AccumulatedDepreciationAccountId, 0, g.Sum(c => c.Amount), memo, null, g.Key.BusinessUnitId)))
                .ToList();
            var journal = await posting.PostAsync(new PostingRequest(entity.Id, periodEnd, Source, run.Id, memo, $"DEP-{periodEnd:yyyy-MM}", lines), ct);
            run.JournalEntryId = journal.Id;
            run.Total = charges.Sum(c => c.Amount);
            run.AssetCount = charges.Count;
        }

        // 6. Save the run and its charges.
        runs.Add(run);
        entryRows.AddRange(rows);
        await uow.SaveChangesAsync(ct);
        var entries = rows.Select(r => { var a = charges.First(c => c.Asset.Id == r.AssetId).Asset; return new DepreciationEntryDto(a.Id, a.Number, a.Name, r.Amount, r.AccumulatedAfter); }).ToList();
        return DepreciationRunDto.From(run, entries, alreadyRun: false);
    }
}
