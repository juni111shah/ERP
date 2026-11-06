using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.Assets.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Features;

/// <summary>LifeMonths is optional: the category's life applies when it is left out.</summary>
public sealed record AssetInput(
    Guid LegalEntityId, string Name, Guid CategoryId, DateOnly AcquisitionDate, decimal Cost, decimal SalvageValue = 0m, Guid? BusinessUnitId = null, string? Custodian = null,
    int? LifeMonths = null, string? Description = null);

public sealed record AssetDto(
    Guid Id, Guid LegalEntityId, string Number, string Name, string? Description, Guid CategoryId, DateOnly AcquisitionDate, decimal Cost, decimal SalvageValue, DepreciationMethod Method, int LifeMonths,
    decimal? DecliningRatePercent, Guid? BusinessUnitId, string? Custodian, AssetStatus Status, decimal AccumulatedDepreciation, DateOnly? CapitalisedOn, CapitalisationSource? CapitalisationSource,
    Guid? CapitalisationJournalId, Guid? SourceBillId, Guid? SourceBillLineId, DateOnly? DisposedOn, decimal? DisposalProceeds, decimal? DisposalGainLoss, Guid? DisposalJournalId)
{
    public decimal NetBookValue => Cost - AccumulatedDepreciation;

    public static AssetDto From(FixedAsset a) => new(
        a.Id, a.LegalEntityId, a.Number, a.Name, a.Description, a.CategoryId, a.AcquisitionDate, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.DecliningRatePercent, a.BusinessUnitId, a.Custodian, a.Status,
        a.AccumulatedDepreciation, a.CapitalisedOn, a.CapitalisationSource, a.CapitalisationJournalId, a.SourceBillId, a.SourceBillLineId, a.DisposedOn, a.DisposalProceeds, a.DisposalGainLoss, a.DisposalJournalId);
}

/// <summary>
/// Clearing: AccountId is the clearing account. Bank: AccountId is the bank or cash account. Vendor: PartyId; an open bill is raised, so the payables ledger stays tied to the control account.
/// Bill: BillId, and BillLineId when the bill has several lines; a line already coded to the asset account needs no entry, any other line is moved to it.
/// </summary>
public sealed record CapitaliseInput(CapitalisationSource Source, DateOnly? Date = null, Guid? AccountId = null, Guid? PartyId = null, Guid? BillId = null, Guid? BillLineId = null, string? Reference = null);

/// <summary>ProceedsAccountId is required when proceeds are above zero. GainLossAccountId defaults to the category's.</summary>
public sealed record DisposeInput(DateOnly Date, decimal Proceeds = 0m, Guid? ProceedsAccountId = null, Guid? GainLossAccountId = null, string? Reason = null);

public sealed record GetAsset(Guid Id) : IQuery<AssetDto>, IRequirePermission { public string RequiredPermission => Permissions.RegisterRead; }
public sealed record CreateAsset(AssetInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
public sealed record UpdateAsset(Guid Id, AssetInput Input) : ICommand<AssetDto>, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
public sealed record DeleteAsset(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
/// <summary>Puts the cost on the books: Dr asset account, Cr what the input names.</summary>
public sealed record CapitaliseAsset(Guid Id, CapitaliseInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterCapitalise; }
/// <summary>Takes the asset off the books: Dr accumulated depreciation and proceeds, Cr cost, and the difference to gain or loss.</summary>
public sealed record DisposeAsset(Guid Id, DisposeInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterDispose; }

public sealed class AssetInputValidator : AbstractValidator<AssetInput>
{
    public AssetInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.AcquisitionDate).MustBePlausibleDate();
        RuleFor(x => x.Cost).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x).Must(x => x.SalvageValue < x.Cost).WithName("salvageValue").WithMessage("The salvage value must be less than the cost.");
        RuleFor(x => x.LifeMonths).InclusiveBetween(1, 1200).When(x => x.LifeMonths is not null).WithMessage("The life must be between 1 and 1200 months.");
        RuleFor(x => x.Custodian).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
public sealed class CreateAssetValidator : AbstractValidator<CreateAsset> { public CreateAssetValidator() => RuleFor(x => x.Input).SetValidator(new AssetInputValidator()); }
public sealed class UpdateAssetValidator : AbstractValidator<UpdateAsset> { public UpdateAssetValidator() => RuleFor(x => x.Input).SetValidator(new AssetInputValidator()); }

public sealed class CapitaliseAssetValidator : AbstractValidator<CapitaliseAsset>
{
    public CapitaliseAssetValidator()
    {
        RuleFor(x => x.Input.Source).IsInEnum();
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Reference).MaximumLength(100);
        RuleFor(x => x.Input.AccountId).NotNull().When(x => x.Input.Source is CapitalisationSource.Clearing or CapitalisationSource.Bank).WithMessage("Choose the account to credit.");
        RuleFor(x => x.Input.PartyId).NotNull().When(x => x.Input.Source == CapitalisationSource.Vendor).WithMessage("Choose the vendor.");
        RuleFor(x => x.Input.BillId).NotNull().When(x => x.Input.Source == CapitalisationSource.Bill).WithMessage("Choose the bill.");
    }
}

public sealed class DisposeAssetValidator : AbstractValidator<DisposeAsset>
{
    public DisposeAssetValidator()
    {
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Proceeds).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Input.ProceedsAccountId).NotNull().When(x => x.Input.Proceeds > 0).WithMessage("Choose the account that received the proceeds.");
        RuleFor(x => x.Input.Reason).MaximumLength(400);
    }
}

internal sealed class AssetHandlers(
    IRepository<FixedAsset> assets, IReadRepository<AssetCategory> categories, IReadRepository<Account> accounts, IReadRepository<Party> parties, IRepository<Bill> bills,
    IReadRepository<BusinessUnit> businessUnits, IReadRepository<DepreciationEntry> entries, ILegalEntityGuard guard, IPostingService posting, INumberingService numbering, IUnitOfWork uow) :
    IRequestHandler<GetAsset, AssetDto>,
    IRequestHandler<CreateAsset, AssetDto>,
    IRequestHandler<UpdateAsset, AssetDto>,
    IRequestHandler<DeleteAsset, Unit>,
    IRequestHandler<CapitaliseAsset, AssetDto>,
    IRequestHandler<DisposeAsset, AssetDto>
{
    private const string SourceCapitalise = "fixedasset";
    private const string SourceDispose = "fixedasset.disposal";

    public async Task<AssetDto> Handle(GetAsset q, CancellationToken ct) => AssetDto.From(await assets.GetAsync(q.Id, ct));

    public async Task<AssetDto> Handle(CreateAsset cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var category = await RequireCategoryAsync(i.CategoryId, ct);
        if (!category.IsActive) throw new FieldValidationException("categoryId", "This category is inactive.");
        await RequireBusinessUnitAsync(i.BusinessUnitId, entity.Id, ct);
        var asset = new FixedAsset
        {
            LegalEntityId = entity.Id, Number = await numbering.NextAsync(AssetsModule.AssetSequence, entity.Id, i.AcquisitionDate, ct), Method = category.Method, DecliningRatePercent = category.DecliningRatePercent,
        };
        Apply(asset, i, category);
        assets.Add(asset);
        await assets.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }

    public async Task<AssetDto> Handle(UpdateAsset cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != asset.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of an asset cannot change.");
        await RequireBusinessUnitAsync(i.BusinessUnitId, asset.LegalEntityId, ct);
        if (asset.Status == AssetStatus.Draft)
        {
            var category = await RequireCategoryAsync(i.CategoryId, ct);
            if (category.Id != asset.CategoryId) { asset.Method = category.Method; asset.DecliningRatePercent = category.DecliningRatePercent; }
            Apply(asset, i, category);
        }
        else
        {
            // The ledger holds these amounts now; only what does not change a figure can still be edited.
            if (i.CategoryId != asset.CategoryId || i.AcquisitionDate != asset.AcquisitionDate || i.Cost != asset.Cost || i.SalvageValue != asset.SalvageValue || (i.LifeMonths is { } life && life != asset.LifeMonths))
                throw new ConflictException("The cost, salvage value, life, category and acquisition date cannot change once an asset is capitalised.");
            asset.Name = i.Name.Trim(); asset.Description = i.Description?.Trim(); asset.BusinessUnitId = i.BusinessUnitId; asset.Custodian = i.Custodian?.Trim();
        }
        await assets.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }

    public async Task<Unit> Handle(DeleteAsset cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status != AssetStatus.Draft) throw new ConflictException("Only an asset that was never capitalised can be deleted. Dispose of it instead.");
        assets.Remove(asset);
        await assets.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<AssetDto> Handle(CapitaliseAsset cmd, CancellationToken ct) => uow.InTransactionAsync(token => CapitaliseCoreAsync(cmd, token), ct);

    private async Task<AssetDto> CapitaliseCoreAsync(CapitaliseAsset cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status != AssetStatus.Draft) throw new ConflictException("This asset is already capitalised.");
        var i = cmd.Input;
        var date = i.Date ?? asset.AcquisitionDate;
        if (date < asset.AcquisitionDate) throw new FieldValidationException("date", "An asset cannot be capitalised before it was acquired.");
        var entity = await guard.RequireAsync(asset.LegalEntityId, ct);
        var category = await RequireCategoryAsync(asset.CategoryId, ct);
        var memo = $"Capitalise {asset.Number} {asset.Name}";
        var reference = i.Reference?.Trim() ?? asset.Number;

        Guid? journalId;
        switch (i.Source)
        {
            case CapitalisationSource.Clearing:
            {
                var account = await accounts.FindAsync(i.AccountId!.Value, ct) ?? throw new FieldValidationException("accountId", "Account not found.");
                if (account.Id == category.AssetAccountId) throw new FieldValidationException("accountId", "Choose an account other than the asset account.");
                journalId = await PostAsync(asset, category.AssetAccountId, account.Id, date, memo, reference, ct);
                break;
            }
            case CapitalisationSource.Bank:
            {
                var account = await accounts.FindAsync(i.AccountId!.Value, ct);
                if (account is null || !BankAccountRules.IsCashOrBank(account)) throw new FieldValidationException("accountId", "Choose an active bank or cash account.");
                journalId = await PostAsync(asset, category.AssetAccountId, account.Id, date, memo, reference, ct);
                break;
            }
            case CapitalisationSource.Vendor:
            {
                var bill = await RaiseBillAsync(asset, category, i, date, memo, entity, ct);
                asset.SourceBillId = bill.Id;
                journalId = bill.JournalEntryId;
                break;
            }
            default:
                journalId = await LinkBillAsync(asset, category, i, date, memo, reference, ct);
                break;
        }

        asset.Status = AssetStatus.Capitalised;
        asset.CapitalisedOn = date;
        asset.CapitalisationSource = i.Source;
        asset.CapitalisationJournalId = journalId;
        await uow.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }

    private async Task<Guid> PostAsync(FixedAsset asset, Guid debit, Guid credit, DateOnly date, string memo, string? reference, CancellationToken ct)
    {
        var journal = await posting.PostAsync(new PostingRequest(asset.LegalEntityId, date, SourceCapitalise, asset.Id, memo, reference,
            [new PostingLine(debit, asset.Cost, 0, memo, null, asset.BusinessUnitId), new PostingLine(credit, 0, asset.Cost, memo, null, asset.BusinessUnitId)]), ct);
        return journal.Id;
    }

    /// <summary>Buying on credit raises a real open bill (coded to the asset account), so the vendor can be paid from it and the ageing still ties to the payables control account.</summary>
    private async Task<Bill> RaiseBillAsync(FixedAsset asset, AssetCategory category, CapitaliseInput i, DateOnly date, string memo, LegalEntity entity, CancellationToken ct)
    {
        var party = await parties.FindAsync(i.PartyId!.Value, ct) ?? throw new FieldValidationException("partyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        var reference = i.Reference?.Trim();
        if (!string.IsNullOrEmpty(reference) && await bills.AnyAsync(b => b.PartyId == party.Id && b.VendorReference == reference && b.Status != DocumentStatus.Voided, ct))
            throw new ConflictException($"A bill with reference '{reference}' from this vendor is already recorded.");
        var ap = await posting.AccountForRoleAsync(SystemRoles.ApControl, ct);
        var bill = new Bill
        {
            LegalEntityId = entity.Id, Number = await numbering.NextAsync("bill", entity.Id, date, ct), PartyId = party.Id, PartyName = party.Name, VendorReference = string.IsNullOrEmpty(reference) ? null : reference,
            BillDate = date, DueDate = date.AddDays(party.PaymentTermsDays ?? 0), CurrencyCode = entity.BaseCurrency, Net = asset.Cost, Total = asset.Cost, Status = DocumentStatus.Issued,
            BusinessUnitId = asset.BusinessUnitId, SourceType = SourceCapitalise, SourceId = asset.Id, Notes = memo,
        };
        bill.Lines.Add(new BillLine { BillId = bill.Id, Seq = 1, Description = asset.Name, Quantity = 1, Price = asset.Cost, Net = asset.Cost, AccountId = category.AssetAccountId });
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, date, "bill", bill.Id, memo, bill.VendorReference ?? bill.Number,
            [new PostingLine(category.AssetAccountId, asset.Cost, 0, memo, null, asset.BusinessUnitId), new PostingLine(ap, 0, asset.Cost, memo, party.Id)], entity.BaseCurrency), ct);
        bill.JournalEntryId = journal.Id;
        bills.Add(bill);
        return bill;
    }

    /// <summary>A posted bill already credited the vendor. If its line went to the asset account the cost is on the books; otherwise the line's amount moves from where it was coded to the asset account.</summary>
    private async Task<Guid?> LinkBillAsync(FixedAsset asset, AssetCategory category, CapitaliseInput i, DateOnly date, string memo, string reference, CancellationToken ct)
    {
        var billId = i.BillId!.Value;
        var bill = await bills.Query(new Specification<Bill>(b => b.Id == billId && b.LegalEntityId == asset.LegalEntityId).Include(b => b.Lines)).FirstOrDefaultAsync(ct)
            ?? throw new FieldValidationException("billId", "Bill not found in this legal entity.");
        if (bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid)) throw new FieldValidationException("billId", "Only a posted bill can be linked.");
        if (date < bill.BillDate) throw new FieldValidationException("date", "The capitalisation date cannot be before the bill date.");
        BillLine? line = i.BillLineId is { } lineId ? bill.Lines.FirstOrDefault(l => l.Id == lineId)
            : bill.Lines.Count == 1 ? bill.Lines[0]
            : bill.Lines.Where(l => l.AccountId == category.AssetAccountId).ToList() is { Count: 1 } onAsset ? onAsset[0] : null;
        if (line is null) throw new FieldValidationException("billLineId", "Choose the line of the bill this asset comes from.");
        var lineAmount = line.Net + (line.TaxRecoverable ? 0 : line.TaxAmount); // tax that cannot be recovered is part of the cost
        var chosen = line.Id;
        var taken = (await assets.Query(new Specification<FixedAsset>(a => a.SourceBillLineId == chosen && a.Id != asset.Id && a.Status != AssetStatus.Draft)).Select(a => a.Cost).ToListAsync(ct)).Sum();
        if (taken + asset.Cost > lineAmount) throw new ConflictException($"Line {line.Seq} of this bill has only {lineAmount - taken:0.00} left to capitalise.");
        asset.SourceBillId = bill.Id;
        asset.SourceBillLineId = line.Id;
        if (line.AccountId == category.AssetAccountId) return bill.JournalEntryId;
        return await PostAsync(asset, category.AssetAccountId, line.AccountId!.Value, date, memo, reference, ct);
    }

    public Task<AssetDto> Handle(DisposeAsset cmd, CancellationToken ct) => uow.InTransactionAsync(token => DisposeCoreAsync(cmd, token), ct);

    private async Task<AssetDto> DisposeCoreAsync(DisposeAsset cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status is not (AssetStatus.Capitalised or AssetStatus.FullyDepreciated)) throw new ConflictException("Only a capitalised asset can be disposed of.");
        var i = cmd.Input;
        if (i.Date < asset.CapitalisedOn) throw new FieldValidationException("date", "An asset cannot be disposed of before it was capitalised.");
        var assetId = asset.Id;
        var lastCharged = await entries.Query(new Specification<DepreciationEntry>(e => e.AssetId == assetId)).OrderByDescending(e => e.PeriodEnd).Select(e => (DateOnly?)e.PeriodEnd).FirstOrDefaultAsync(ct);
        if (lastCharged is { } charged && i.Date <= charged) throw new ConflictException($"This asset has been depreciated through {charged:yyyy-MM-dd}. Choose a later disposal date.");
        // Net book value has to be current, or the gain or loss would be wrong: every month before the disposal month must have been charged.
        var dueThrough = new DateOnly(i.Date.Year, i.Date.Month, 1).AddDays(-1);
        var due = DepreciationCalculator.AccumulatedThrough(DepreciationCalculator.PlanOf(asset), dueThrough);
        if (asset.AccumulatedDepreciation < due) throw new ConflictException($"Run depreciation through {dueThrough:yyyy-MM} before disposing of this asset.");

        var category = await RequireCategoryAsync(asset.CategoryId, ct);
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

        asset.Status = AssetStatus.Disposed;
        asset.DisposedOn = i.Date;
        asset.DisposalProceeds = i.Proceeds;
        asset.DisposalGainLoss = gain;
        asset.DisposalJournalId = journal.Id;
        asset.DisposalReason = i.Reason?.Trim();
        await uow.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }

    private async Task<AssetCategory> RequireCategoryAsync(Guid id, CancellationToken ct)
        => await categories.FindAsync(id, ct) ?? throw new FieldValidationException("categoryId", "Asset category not found.");

    private async Task RequireBusinessUnitAsync(Guid? id, Guid legalEntityId, CancellationToken ct)
    {
        if (id is { } unitId && !await businessUnits.AnyAsync(u => u.Id == unitId && u.LegalEntityId == legalEntityId, ct))
            throw new FieldValidationException("businessUnitId", "Business unit not found in this legal entity.");
    }

    private static void Apply(FixedAsset a, AssetInput i, AssetCategory category)
    {
        a.Name = i.Name.Trim(); a.Description = i.Description?.Trim(); a.CategoryId = category.Id; a.AcquisitionDate = i.AcquisitionDate; a.Cost = i.Cost; a.SalvageValue = i.SalvageValue;
        a.LifeMonths = i.LifeMonths ?? (a.LifeMonths > 0 && a.CategoryId == category.Id ? a.LifeMonths : category.LifeMonths); a.BusinessUnitId = i.BusinessUnitId; a.Custodian = i.Custodian?.Trim();
    }
}

/// <summary>A capitalised asset depends on its bill: voiding the bill would take the cost off the books under the asset.</summary>
public sealed class AssetBillGuard(IReadRepository<FixedAsset> assets) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        if (await assets.AnyAsync(a => a.SourceBillId == n.BillId && a.Status != AssetStatus.Draft, ct))
            throw new ConflictException("A fixed asset was capitalised from this bill, so it cannot be voided. Dispose of the asset first.");
    }
}
