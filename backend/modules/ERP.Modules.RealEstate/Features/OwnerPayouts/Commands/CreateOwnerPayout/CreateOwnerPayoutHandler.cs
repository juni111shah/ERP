using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateOwnerPayoutHandler(
    IRepository<OwnerPayout> payouts, IReadRepository<Account> accounts, IReadRepository<Party> parties, OwnerStatementBuilder statements, OwnerAccounts ownerAccounts,
    ILegalEntityGuard guard, INumberingService numbering, IPostingService posting, ISender sender, IUnitOfWork uow)
    : ICommandHandler<CreateOwnerPayoutCommand, OwnerPayoutDto>
{
    public const string SourceType = "realestate.ownerpayout";

    public Task<OwnerPayoutDto> Handle(CreateOwnerPayoutCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The legal entity, the owner, the bank account and no overlapping live payout.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        var owner = await parties.FindAsync(i.OwnerPartyId, token) ?? throw new FieldValidationException("ownerPartyId", "Owner not found.");
        if (i.Method == OwnerPayoutMethod.Payment)
        {
            var bank = await accounts.FindAsync(i.BankAccountId!.Value, token);
            if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        }
        if (await payouts.AnyAsync(x => x.OwnerPartyId == owner.Id && x.LegalEntityId == entity.Id && x.Status == OwnerPayoutStatus.Posted && x.PeriodFrom <= i.To && x.PeriodTo >= i.From, token))
            throw new ConflictException("This owner already has a payout overlapping that period. Void it first, or choose the next period.");

        // 2. What the owner funds of the period come to.
        var st = await statements.BuildAsync(owner.Id, i.From, i.To, entity.Id, ownerFundsOnly: true, token);
        var net = st.NetToOwner;
        if (net <= 0) throw new ConflictException($"Nothing to pay for that period: rent collected {st.RentCollected:0.00} less fee {st.ManagementFee:0.00} and maintenance {st.MaintenanceCosts:0.00}. A shortfall carries on: choose a longer period.");

        // 3. The payout.
        var number = await numbering.NextAsync(Docs.OwnerPayout, entity.Id, i.Date, token);
        var payout = new OwnerPayout
        {
            LegalEntityId = entity.Id, Number = number, OwnerPartyId = owner.Id, PeriodFrom = i.From, PeriodTo = i.To, Date = i.Date, Method = i.Method, Status = OwnerPayoutStatus.Posted,
            RentCollected = st.RentCollected, ManagementFee = st.ManagementFee, MaintenanceCosts = st.MaintenanceCosts, Amount = net, BankAccountId = i.Method == OwnerPayoutMethod.Payment ? i.BankAccountId : null, Notes = i.Notes?.Trim(),
        };
        payouts.Add(payout);

        // 4. The fee and the owner's maintenance leave the payable.
        var payable = await ownerAccounts.PayableAsync(token);
        var memo = $"Owner payout {number}, {owner.Name}";
        var feeLines = new List<PostingLine>();
        if (st.ManagementFee > 0) feeLines.Add(new PostingLine(await ownerAccounts.FeeIncomeAsync(token), 0, st.ManagementFee, memo));
        if (st.MaintenanceCosts > 0) feeLines.Add(new PostingLine(await ownerAccounts.RechargeAsync(token), 0, st.MaintenanceCosts, memo));
        var kept = st.ManagementFee + st.MaintenanceCosts;

        // 5. Paid from the bank now, or billed to be paid by accounts payable.
        if (i.Method == OwnerPayoutMethod.Payment)
        {
            List<PostingLine> lines = [new PostingLine(payable, st.RentCollected, 0, memo, owner.Id), .. feeLines, new PostingLine(i.BankAccountId!.Value, 0, net, memo)];
            payout.JournalEntryId = (await posting.PostAsync(new PostingRequest(entity.Id, i.Date, SourceType, payout.Id, memo, number, lines), token)).Id;
        }
        else
        {
            if (kept > 0)
            {
                List<PostingLine> lines = [new PostingLine(payable, kept, 0, memo, owner.Id), .. feeLines];
                payout.JournalEntryId = (await posting.PostAsync(new PostingRequest(entity.Id, i.Date, SourceType, payout.Id, memo, number, lines), token)).Id;
            }
            // the bill debits the payable and credits accounts payable, so the owner is paid and aged like any vendor
            var bill = await sender.Send(new CreateBillCommand(new BillInput(entity.Id, owner.Id, number, i.Date, i.DueDate,
                [new BillLineInput($"Owner payout {number}, {i.From:dd MMM yyyy} to {i.To:dd MMM yyyy}", 1, net, payable)], i.Notes?.Trim())), token);
            payout.BillId = bill.Id;
            await sender.Send(new PostBillCommand(bill.Id), token);
        }

        // 6. Save.
        await payouts.SaveChangesAsync(token);
        return OwnerPayoutDto.From(payout);
    }, ct);
}
