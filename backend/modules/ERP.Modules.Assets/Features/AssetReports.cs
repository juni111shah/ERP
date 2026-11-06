using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Dashboard;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modules.Assets.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Features;

public sealed record AssetRegisterRow(
    Guid AssetId, Guid LegalEntityId, string Number, string Name, Guid CategoryId, string CategoryName, Guid? BusinessUnitId, string? Custodian, DateOnly AcquisitionDate, DateOnly CapitalisedOn,
    decimal Cost, decimal SalvageValue, decimal AccumulatedDepreciation, decimal NetBookValue);
public sealed record AssetRegisterDto(DateOnly AsOf, IReadOnlyList<AssetRegisterRow> Rows, decimal TotalCost, decimal TotalAccumulatedDepreciation, decimal TotalNetBookValue);

/// <summary>The assets on the books at the end of a date, valued from the depreciation actually posted by then. Disposed assets leave on their disposal date.</summary>
public sealed record GetAssetRegister(DateOnly AsOf, Guid? LegalEntityId = null, Guid? CategoryId = null) : IQuery<AssetRegisterDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }

/// <summary>Posted = charged by the run in JournalEntryId (PostedAmount can exceed Amount when the run caught up a skipped month). Planned = what the schedule still expects.</summary>
public sealed record AssetScheduleRow(int Year, int Month, DateOnly PeriodEnd, decimal Amount, decimal Accumulated, decimal NetBookValue, bool Posted, decimal? PostedAmount, Guid? RunId, Guid? JournalEntryId);
public sealed record AssetScheduleDto(Guid AssetId, string Number, string Name, decimal Cost, decimal SalvageValue, DepreciationMethod Method, int LifeMonths, AssetStatus Status, decimal AccumulatedDepreciation, IReadOnlyList<AssetScheduleRow> Rows);

/// <summary>The month-by-month schedule of one asset, marking what has been posted.</summary>
public sealed record GetAssetSchedule(Guid AssetId) : IQuery<AssetScheduleDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }

public sealed record DepreciationPeriodRow(int Year, int Month, decimal Posted, decimal Planned);
public sealed record DepreciationForecastDto(DateOnly From, DateOnly To, IReadOnlyList<DepreciationPeriodRow> Rows, decimal TotalPosted, decimal TotalPlanned);

/// <summary>Depreciation by month across a legal entity's assets: posted runs up to the last run, and what the schedules still expect after it.</summary>
public sealed record GetDepreciationSchedule(DateOnly From, DateOnly To, Guid? LegalEntityId = null) : IQuery<DepreciationForecastDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }

public sealed class GetAssetRegisterValidator : AbstractValidator<GetAssetRegister>
{
    public GetAssetRegisterValidator() => RuleFor(x => x.AsOf).MustBePlausibleDate();
}

public sealed class GetDepreciationScheduleValidator : AbstractValidator<GetDepreciationSchedule>
{
    public GetDepreciationScheduleValidator()
    {
        RuleFor(x => x.From).MustBePlausibleDate();
        RuleFor(x => x.To).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.From <= x.To).WithName("to").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= PartyLedger.MaxDays).WithName("to").WithMessage("The date range can be at most 10 years.");
    }
}

internal sealed class AssetReportHandlers(
    IReadRepository<FixedAsset> assets, IReadRepository<AssetCategory> categories, IReadRepository<DepreciationEntry> entries, IReadRepository<DepreciationRun> runs, IReportCurrencyGuard currencies) :
    IRequestHandler<GetAssetRegister, AssetRegisterDto>,
    IRequestHandler<GetAssetSchedule, AssetScheduleDto>,
    IRequestHandler<GetDepreciationSchedule, DepreciationForecastDto>
{
    public async Task<AssetRegisterDto> Handle(GetAssetRegister q, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var onBooks = await assets.Query(new Specification<FixedAsset>(a => a.CapitalisedOn != null && a.CapitalisedOn <= q.AsOf && (a.DisposedOn == null || a.DisposedOn > q.AsOf)
                && (q.LegalEntityId == null || a.LegalEntityId == q.LegalEntityId) && (q.CategoryId == null || a.CategoryId == q.CategoryId)))
            .OrderBy(a => a.Number).ToListAsync(ct);
        var ids = onBooks.Select(a => a.Id).ToList();
        var charged = (await entries.Query(new Specification<DepreciationEntry>(e => ids.Contains(e.AssetId) && e.PeriodEnd <= q.AsOf)).Select(e => new { e.AssetId, e.Amount }).ToListAsync(ct))
            .GroupBy(e => e.AssetId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var names = await categories.Query().Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var rows = onBooks.Select(a =>
        {
            var accumulated = charged.GetValueOrDefault(a.Id);
            return new AssetRegisterRow(a.Id, a.LegalEntityId, a.Number, a.Name, a.CategoryId, names.GetValueOrDefault(a.CategoryId, "?"), a.BusinessUnitId, a.Custodian, a.AcquisitionDate, a.CapitalisedOn!.Value, a.Cost, a.SalvageValue, accumulated, a.Cost - accumulated);
        }).ToList();
        return new AssetRegisterDto(q.AsOf, rows, rows.Sum(r => r.Cost), rows.Sum(r => r.AccumulatedDepreciation), rows.Sum(r => r.NetBookValue));
    }

    public async Task<AssetScheduleDto> Handle(GetAssetSchedule q, CancellationToken ct)
    {
        var a = await assets.GetAsync(q.AssetId, ct);
        var posted = await entries.Query(new Specification<DepreciationEntry>(e => e.AssetId == q.AssetId)).ToListAsync(ct);
        var runIds = posted.Select(p => p.RunId).ToList();
        var journals = await runs.Query(new Specification<DepreciationRun>(r => runIds.Contains(r.Id))).Select(r => new { r.Id, r.JournalEntryId }).ToDictionaryAsync(r => r.Id, r => r.JournalEntryId, ct);
        var lastPosted = posted.Count == 0 ? (DateOnly?)null : posted.Max(p => p.PeriodEnd);
        var rows = DepreciationCalculator.Schedule(DepreciationCalculator.PlanOf(a))
            .Where(p => a.DisposedOn is not { } gone || p.PeriodEnd <= gone || (lastPosted is { } l && p.PeriodEnd <= l))
            .Select(p =>
            {
                var entry = posted.FirstOrDefault(e => e.PeriodEnd == p.PeriodEnd);
                return new AssetScheduleRow(p.Year, p.Month, p.PeriodEnd, p.Amount, p.Accumulated, p.NetBookValue, lastPosted is { } last && p.PeriodEnd <= last, entry?.Amount, entry?.RunId, entry is null ? null : journals.GetValueOrDefault(entry.RunId));
            }).ToList();
        return new AssetScheduleDto(a.Id, a.Number, a.Name, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.Status, a.AccumulatedDepreciation, rows);
    }

    public async Task<DepreciationForecastDto> Handle(GetDepreciationSchedule q, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var from = new DateOnly(q.From.Year, q.From.Month, 1);
        var to = DepreciationCalculator.EndOfMonth(q.To.Year, q.To.Month);
        var posted = await entries.Query(new Specification<DepreciationEntry>(e => e.PeriodEnd >= from && e.PeriodEnd <= to && (q.LegalEntityId == null || e.LegalEntityId == q.LegalEntityId))).Select(e => new { e.PeriodEnd, e.Amount }).ToListAsync(ct);
        var open = await assets.Query(new Specification<FixedAsset>(a => (a.Status == AssetStatus.Capitalised) && (q.LegalEntityId == null || a.LegalEntityId == q.LegalEntityId))).ToListAsync(ct);

        var planned = new Dictionary<DateOnly, decimal>();
        foreach (var a in open)
            foreach (var p in DepreciationCalculator.Schedule(DepreciationCalculator.PlanOf(a)).Where(p => p.Accumulated > a.AccumulatedDepreciation && p.PeriodEnd >= from && p.PeriodEnd <= to))
                planned[p.PeriodEnd] = planned.GetValueOrDefault(p.PeriodEnd) + p.Amount;

        var rows = new List<DepreciationPeriodRow>();
        for (var month = from; month <= to; month = month.AddMonths(1))
        {
            var end = DepreciationCalculator.EndOfMonth(month.Year, month.Month);
            var (done, ahead) = (posted.Where(p => p.PeriodEnd == end).Sum(p => p.Amount), planned.GetValueOrDefault(end));
            if (done != 0 || ahead != 0) rows.Add(new DepreciationPeriodRow(month.Year, month.Month, done, ahead));
        }
        return new DepreciationForecastDto(from, to, rows, rows.Sum(r => r.Posted), rows.Sum(r => r.Planned));
    }
}

/// <summary>Net book value of the assets in scope at the end of the period, from the register and the depreciation actually posted, not a stored total.</summary>
public sealed class AssetsDashboard(IReadRepository<FixedAsset> assets, IReadRepository<DepreciationEntry> entries, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => AssetsModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var kpis = new List<KpiCard>();
        if (user.HasPermission(Permissions.RegisterRead))
        {
            var (entity, asOf) = (scope.LegalEntityId, scope.To);
            var onBooks = await assets.Query(new Specification<FixedAsset>(a => a.CapitalisedOn != null && a.CapitalisedOn <= asOf && (a.DisposedOn == null || a.DisposedOn > asOf) && (entity == null || a.LegalEntityId == entity)))
                .Select(a => new { a.Id, a.Cost }).ToListAsync(ct);
            var ids = onBooks.Select(a => a.Id).ToList();
            var charged = await entries.Query(new Specification<DepreciationEntry>(e => ids.Contains(e.AssetId) && e.PeriodEnd <= asOf)).Select(e => e.Amount).ToListAsync(ct);
            kpis.Add(new("assets-net-book-value", "Fixed assets (net book value)", onBooks.Sum(a => a.Cost) - charged.Sum(), "money", $"{onBooks.Count} assets on the books", 160, "/assets/register"));
        }
        return new DashboardContribution(kpis, [], []);
    }
}
