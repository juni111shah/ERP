using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Assets.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Features;

public sealed record DepreciationEntryDto(Guid AssetId, string Number, string Name, decimal Amount, decimal AccumulatedAfter);

public sealed record DepreciationRunDto(
    Guid Id, Guid LegalEntityId, int Year, int Month, DateOnly PeriodEnd, Guid? JournalEntryId, decimal Total, int AssetCount, DateTime RunAt, Guid? RunBy,
    IReadOnlyList<DepreciationEntryDto>? Entries, bool AlreadyRun);

public sealed record GetDepreciationRun(Guid Id) : IQuery<DepreciationRunDto>, IRequirePermission { public string RequiredPermission => Permissions.DepreciationRead; }

/// <summary>
/// Charges one month for a legal entity: one journal (Dr depreciation expense, Cr accumulated depreciation), dated the last day of the month, grouped by account and business unit.
/// Running a period again changes nothing and returns the first run. A skipped month is caught up by the next run. Depreciation stops at the salvage value.
/// </summary>
public sealed record RunDepreciation(Guid LegalEntityId, int Year, int Month) : ICommand<DepreciationRunDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => Permissions.DepreciationRun;
}

public sealed class RunDepreciationValidator : AbstractValidator<RunDepreciation>
{
    public RunDepreciationValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

internal sealed class DepreciationHandlers(
    IRepository<DepreciationRun> runs, IRepository<DepreciationEntry> entryRows, IRepository<FixedAsset> assets, IReadRepository<AssetCategory> categories,
    ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow, ICurrentUser user, IClock clock) :
    IRequestHandler<RunDepreciation, DepreciationRunDto>,
    IRequestHandler<GetDepreciationRun, DepreciationRunDto>
{
    private const string Source = "fixedasset.depreciation";

    public async Task<DepreciationRunDto> Handle(GetDepreciationRun q, CancellationToken ct)
    {
        var run = await runs.GetAsync(q.Id, ct);
        var rows = await entryRows.Query(new Specification<DepreciationEntry>(e => e.RunId == q.Id)).ToListAsync(ct);
        var ids = rows.Select(r => r.AssetId).ToList();
        var names = await assets.Query(new Specification<FixedAsset>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Number, a.Name }).ToDictionaryAsync(a => a.Id, ct);
        var entries = rows.Select(r => new DepreciationEntryDto(r.AssetId, names.GetValueOrDefault(r.AssetId)?.Number ?? "?", names.GetValueOrDefault(r.AssetId)?.Name ?? "?", r.Amount, r.AccumulatedAfter)).OrderBy(e => e.Number).ToList();
        return ToDto(run, entries, alreadyRun: true);
    }

    public Task<DepreciationRunDto> Handle(RunDepreciation cmd, CancellationToken ct) => uow.InTransactionAsync(token => RunCoreAsync(cmd, token), ct);

    private async Task<DepreciationRunDto> RunCoreAsync(RunDepreciation cmd, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(cmd.LegalEntityId, ct);
        var existing = await runs.Query(new Specification<DepreciationRun>(r => r.LegalEntityId == entity.Id && r.Year == cmd.Year && r.Month == cmd.Month)).FirstOrDefaultAsync(ct);
        if (existing is not null) return ToDto(existing, null, alreadyRun: true);

        var periodEnd = DepreciationCalculator.EndOfMonth(cmd.Year, cmd.Month);
        if (periodEnd > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("month", $"{periodEnd:yyyy-MM} has not ended yet.");
        var later = await runs.Query(new Specification<DepreciationRun>(r => r.LegalEntityId == entity.Id && r.PeriodEnd > periodEnd)).OrderByDescending(r => r.PeriodEnd).Select(r => (DateOnly?)r.PeriodEnd).FirstOrDefaultAsync(ct);
        if (later is { } latest) throw new ConflictException($"Depreciation has already run for {latest:yyyy-MM}. Runs go forward: a skipped month was caught up then.");

        var due = await assets.Tracked(new Specification<FixedAsset>(a => a.LegalEntityId == entity.Id && a.Status == AssetStatus.Capitalised && a.CapitalisedOn <= periodEnd)).ToListAsync(ct);
        var categoryIds = due.Select(a => a.CategoryId).Distinct().ToList();
        var byCategory = await categories.Query(new Specification<AssetCategory>(c => categoryIds.Contains(c.Id))).ToDictionaryAsync(c => c.Id, ct);

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
        runs.Add(run);
        entryRows.AddRange(rows);
        await uow.SaveChangesAsync(ct);
        var entries = rows.Select(r => { var a = charges.First(c => c.Asset.Id == r.AssetId).Asset; return new DepreciationEntryDto(a.Id, a.Number, a.Name, r.Amount, r.AccumulatedAfter); }).ToList();
        return ToDto(run, entries, alreadyRun: false);
    }

    private static DepreciationRunDto ToDto(DepreciationRun r, IReadOnlyList<DepreciationEntryDto>? entries, bool alreadyRun)
        => new(r.Id, r.LegalEntityId, r.Year, r.Month, r.PeriodEnd, r.JournalEntryId, r.Total, r.AssetCount, r.RunAt, r.RunBy, entries, alreadyRun);
}
