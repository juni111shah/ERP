using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class DisposeAssetHandler(IFixedAssetRepository assets, IDepreciationEntryRepository entries, FixedAssetChecks checks, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<DisposeAssetCommand, AssetDto>
{
    private const string SourceDispose = "fixedasset.disposal";

    public Task<AssetDto> Handle(DisposeAssetCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => DisposeCoreAsync(cmd, token), ct);

    private async Task<AssetDto> DisposeCoreAsync(DisposeAssetCommand cmd, CancellationToken ct)
    {
        // 1. Load the capitalised asset; the disposal date must come after its capitalisation and its last charge.
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status is not (AssetStatus.Capitalised or AssetStatus.FullyDepreciated)) throw new ConflictException("Only a capitalised asset can be disposed of.");
        var i = cmd.Input;
        if (i.Date < asset.CapitalisedOn) throw new FieldValidationException("date", "An asset cannot be disposed of before it was capitalised.");
        var assetId = asset.Id;
        var lastCharged = await entries.LastPeriodEndForAssetAsync(assetId, ct);
        if (lastCharged is { } charged && i.Date <= charged) throw new ConflictException($"This asset has been depreciated through {charged:yyyy-MM-dd}. Choose a later disposal date.");
        // Net book value has to be current, or the gain or loss would be wrong: every month before the disposal month must have been charged.
        var dueThrough = new DateOnly(i.Date.Year, i.Date.Month, 1).AddDays(-1);
        var due = DepreciationCalculator.AccumulatedThrough(DepreciationCalculator.PlanOf(asset), dueThrough);
        if (asset.AccumulatedDepreciation < due) throw new ConflictException($"Run depreciation through {dueThrough:yyyy-MM} before disposing of this asset.");

        // 2. Post: Dr accumulated depreciation and proceeds, Cr cost, the difference to gain or loss.
        var category = await checks.RequireCategoryAsync(asset.CategoryId, ct);
        var netBookValue = asset.Cost - asset.AccumulatedDepreciation;
        var gain = i.Proceeds - netBookValue;
        var memo = $"Dispose {asset.Number} {asset.Name}";
        var unit = asset.BusinessUnitId;
        var lines = new List<PostingLine>();
        if (asset.AccumulatedDepreciation > 0) lines.Add(new PostingLine(category.AccumulatedDepreciationAccountId, asset.AccumulatedDepreciation, 0, memo, null, unit));
        if (i.Proceeds > 0) lines.Add(new PostingLine(i.ProceedsAccountId!.Value, i.Proceeds, 0, memo, null, unit));
        lines.Add(new PostingLine(category.AssetAccountId, 0, asset.Cost, memo, null, unit));
        if (gain != 0)
        {
            var gainLoss = i.GainLossAccountId ?? category.DisposalGainLossAccountId ?? throw new FieldValidationException("gainLossAccountId", "Choose the account for the gain or loss on disposal.");
            lines.Add(gain > 0 ? new PostingLine(gainLoss, 0, gain, memo, null, unit) : new PostingLine(gainLoss, -gain, 0, memo, null, unit));
        }
        var journal = await posting.PostAsync(new PostingRequest(asset.LegalEntityId, i.Date, SourceDispose, asset.Id, memo, asset.Number, lines), ct);

        // 3. The asset is disposed; save.
        asset.Status = AssetStatus.Disposed;
        asset.DisposedOn = i.Date;
        asset.DisposalProceeds = i.Proceeds;
        asset.DisposalGainLoss = gain;
        asset.DisposalJournalId = journal.Id;
        asset.DisposalReason = i.Reason?.Trim();
        await uow.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }
}
