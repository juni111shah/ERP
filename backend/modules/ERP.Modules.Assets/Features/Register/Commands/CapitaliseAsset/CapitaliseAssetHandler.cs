using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class CapitaliseAssetHandler(
    IFixedAssetRepository assets, FixedAssetChecks checks, IReadRepository<Account> accounts, IReadRepository<Party> parties, IBillRepository bills,
    ILegalEntityGuard guard, IPostingService posting, INumberingService numbering, IUnitOfWork uow) : ICommandHandler<CapitaliseAssetCommand, AssetDto>
{
    private const string SourceCapitalise = "fixedasset";

    public Task<AssetDto> Handle(CapitaliseAssetCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => CapitaliseCoreAsync(cmd, token), ct);

    private async Task<AssetDto> CapitaliseCoreAsync(CapitaliseAssetCommand cmd, CancellationToken ct)
    {
        // 1. Load the draft asset; it cannot go on the books before it was acquired.
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status != AssetStatus.Draft) throw new ConflictException("This asset is already capitalised.");
        var i = cmd.Input;
        var date = i.Date ?? asset.AcquisitionDate;
        if (date < asset.AcquisitionDate) throw new FieldValidationException("date", "An asset cannot be capitalised before it was acquired.");
        var entity = await guard.RequireAsync(asset.LegalEntityId, ct);
        var category = await checks.RequireCategoryAsync(asset.CategoryId, ct);
        var memo = $"Capitalise {asset.Number} {asset.Name}";
        var reference = i.Reference?.Trim() ?? asset.Number;

        // 2. Post the cost against what the source names.
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

        // 3. The asset is capitalised; save.
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
        var bill = await bills.FindWithLinesInEntityAsync(billId, asset.LegalEntityId, ct)
            ?? throw new FieldValidationException("billId", "Bill not found in this legal entity.");
        if (bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid)) throw new FieldValidationException("billId", "Only a posted bill can be linked.");
        if (date < bill.BillDate) throw new FieldValidationException("date", "The capitalisation date cannot be before the bill date.");
        BillLine? line = i.BillLineId is { } lineId ? bill.Lines.FirstOrDefault(l => l.Id == lineId)
            : bill.Lines.Count == 1 ? bill.Lines[0]
            : bill.Lines.Where(l => l.AccountId == category.AssetAccountId).ToList() is { Count: 1 } onAsset ? onAsset[0] : null;
        if (line is null) throw new FieldValidationException("billLineId", "Choose the line of the bill this asset comes from.");
        var lineAmount = line.Net + (line.TaxRecoverable ? 0 : line.TaxAmount); // tax that cannot be recovered is part of the cost
        var chosen = line.Id;
        var taken = await assets.CostCapitalisedFromBillLineAsync(chosen, asset.Id, ct);
        if (taken + asset.Cost > lineAmount) throw new ConflictException($"Line {line.Seq} of this bill has only {lineAmount - taken:0.00} left to capitalise.");
        asset.SourceBillId = bill.Id;
        asset.SourceBillLineId = line.Id;
        if (line.AccountId == category.AssetAccountId) return bill.JournalEntryId;
        return await PostAsync(asset, category.AssetAccountId, line.AccountId!.Value, date, memo, reference, ct);
    }
}
