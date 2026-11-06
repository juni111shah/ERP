using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Method Payment needs BankAccountId; method Bill raises a vendor bill to the owner (DueDate is optional) that accounts payable pays later.</summary>
public sealed record OwnerPayoutInput(Guid LegalEntityId, Guid OwnerPartyId, DateOnly From, DateOnly To, DateOnly Date, OwnerPayoutMethod Method = OwnerPayoutMethod.Payment, Guid? BankAccountId = null, DateOnly? DueDate = null, string? Notes = null);

public sealed record OwnerPayoutDto(
    Guid Id, string Number, Guid LegalEntityId, Guid OwnerPartyId, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly Date, OwnerPayoutMethod Method, OwnerPayoutStatus Status,
    decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal Amount, Guid? BankAccountId, Guid? BillId, Guid? JournalEntryId, string? Notes, string? VoidReason, DateTime? VoidedAt);

public sealed record GetOwnerPayout(Guid Id) : IQuery<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsRead; }

/// <summary>Pays the owner what the statement of a period shows for the rent held in the owner payable, clearing it: the fee and the owner's maintenance leave the payable at the same time. One live payout per owner, entity and period.</summary>
public sealed record CreateOwnerPayout(OwnerPayoutInput Input) : ICommand<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsManage; }

/// <summary>Reverses a payout: its journal, and on the bill route its bill too (refused once the bill has been paid). The period can then be paid again.</summary>
public sealed record VoidOwnerPayout(Guid Id, string? Reason) : ICommand<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsManage; }

public sealed class CreateOwnerPayoutValidator : AbstractValidator<CreateOwnerPayout>
{
    public CreateOwnerPayoutValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.OwnerPartyId).NotEmpty();
        RuleFor(x => x.Input.To).GreaterThanOrEqualTo(x => x.Input.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Method).IsInEnum();
        RuleFor(x => x.Input.BankAccountId).NotEmpty().When(x => x.Input.Method == OwnerPayoutMethod.Payment).WithMessage("Choose the bank account the owner is paid from.");
        RuleFor(x => x.Input.Notes).MaximumLength(500);
    }
}

public sealed class VoidOwnerPayoutValidator : AbstractValidator<VoidOwnerPayout>
{
    public VoidOwnerPayoutValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}

/// <summary>The accounts that hold owner money. Workspaces created before the owner payable existed get them on first use, so billing a managed property never stops for a missing account.</summary>
internal sealed class OwnerAccounts(IRepository<Account> accounts)
{
    public const string PayableRole = "realestate.owner.payable";
    public const string FeeIncomeRole = "realestate.income.management";
    public const string RechargeRole = "realestate.income.recharges";

    public Task<Guid> PayableAsync(CancellationToken ct) => EnsureAsync(PayableRole, "2210", "Owner funds payable", AccountType.Liability, "Current liabilities", ct);
    public Task<Guid> FeeIncomeAsync(CancellationToken ct) => EnsureAsync(FeeIncomeRole, "4020", "Management fee income", AccountType.Income, "Revenue", ct);
    public Task<Guid> RechargeAsync(CancellationToken ct) => EnsureAsync(RechargeRole, "4030", "Maintenance recharged to owners", AccountType.Income, "Other income", ct);

    private async Task<Guid> EnsureAsync(string role, string code, string name, AccountType type, string group, CancellationToken ct)
    {
        var id = await accounts.Query(new Specification<Account>(a => a.SystemRole == role && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        if (id is { } found) return found;
        var free = code;
        while (await accounts.AnyAsync(a => a.Code == free, ct)) free = (int.Parse(free) + 1).ToString();
        var account = new Account { Code = free, Name = name, Type = type, SystemRole = role, ReportingGroup = group, IsSystem = true, IsPostable = true, IsActive = true };
        accounts.Add(account);
        await accounts.SaveChangesAsync(ct); // the posting service reads accounts from the database
        return account.Id;
    }
}

internal sealed class OwnerPayoutHandlers(
    IRepository<OwnerPayout> payouts, IRepository<JournalEntry> journals, IReadRepository<Account> accounts, IReadRepository<Bill> bills, IReadRepository<Party> parties, IRepository<AuditEvent> audit,
    OwnerStatementBuilder statements, OwnerAccounts ownerAccounts, ILegalEntityGuard guard, INumberingService numbering, IPostingService posting, ISender sender, ICurrentUser user, IClock clock, IUnitOfWork uow) :
    IRequestHandler<GetOwnerPayout, OwnerPayoutDto>,
    IRequestHandler<CreateOwnerPayout, OwnerPayoutDto>,
    IRequestHandler<VoidOwnerPayout, OwnerPayoutDto>,
    INotificationHandler<BillReleased>
{
    public const string SourceType = "realestate.ownerpayout";

    public async Task<OwnerPayoutDto> Handle(GetOwnerPayout q, CancellationToken ct) => ToDto(await payouts.GetAsync(q.Id, ct));

    public Task<OwnerPayoutDto> Handle(CreateOwnerPayout cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
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

        var st = await statements.BuildAsync(owner.Id, i.From, i.To, entity.Id, ownerFundsOnly: true, token);
        var net = st.NetToOwner;
        if (net <= 0) throw new ConflictException($"Nothing to pay for that period: rent collected {st.RentCollected:0.00} less fee {st.ManagementFee:0.00} and maintenance {st.MaintenanceCosts:0.00}. A shortfall carries on: choose a longer period.");

        var number = await numbering.NextAsync(Docs.OwnerPayout, entity.Id, i.Date, token);
        var payout = new OwnerPayout
        {
            LegalEntityId = entity.Id, Number = number, OwnerPartyId = owner.Id, PeriodFrom = i.From, PeriodTo = i.To, Date = i.Date, Method = i.Method, Status = OwnerPayoutStatus.Posted,
            RentCollected = st.RentCollected, ManagementFee = st.ManagementFee, MaintenanceCosts = st.MaintenanceCosts, Amount = net, BankAccountId = i.Method == OwnerPayoutMethod.Payment ? i.BankAccountId : null, Notes = i.Notes?.Trim(),
        };
        payouts.Add(payout);

        var payable = await ownerAccounts.PayableAsync(token);
        var memo = $"Owner payout {number}, {owner.Name}";
        var feeLines = new List<PostingLine>();
        if (st.ManagementFee > 0) feeLines.Add(new PostingLine(await ownerAccounts.FeeIncomeAsync(token), 0, st.ManagementFee, memo));
        if (st.MaintenanceCosts > 0) feeLines.Add(new PostingLine(await ownerAccounts.RechargeAsync(token), 0, st.MaintenanceCosts, memo));
        var kept = st.ManagementFee + st.MaintenanceCosts;

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
            var bill = await sender.Send(new CreateBill(new BillInput(entity.Id, owner.Id, number, i.Date, i.DueDate,
                [new BillLineInput($"Owner payout {number}, {i.From:dd MMM yyyy} to {i.To:dd MMM yyyy}", 1, net, payable)], i.Notes?.Trim())), token);
            payout.BillId = bill.Id;
            await sender.Send(new PostBill(bill.Id), token);
        }
        await payouts.SaveChangesAsync(token);
        return ToDto(payout);
    }, ct);

    public Task<OwnerPayoutDto> Handle(VoidOwnerPayout cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var payout = await payouts.GetForUpdateAsync(cmd.Id, token);
        if (payout.Status == OwnerPayoutStatus.Voided) throw new ConflictException("This payout is already voided.");
        if (payout.BillId is { } billId)
        {
            // voiding or deleting the bill raises BillReleased, which reverses the rest of the payout below
            var bill = await bills.FindAsync(billId, token);
            if (bill?.Status is DocumentStatus.Draft or DocumentStatus.PendingApproval) await sender.Send(new DeleteBill(billId), token);
            else await sender.Send(new VoidBill(billId, cmd.Reason), token);
        }
        else await ReleaseAsync(payout, token);
        VoidMemo.Stamp(payout, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(OwnerPayout), payout.Id, payout.Number, cmd.Reason!);
        await payouts.SaveChangesAsync(token);
        return ToDto(payout);
    }, ct);

    /// <summary>The owner's bill was voided or deleted (here or on the bills screen): the payout goes with it, so the period can be paid again.</summary>
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var payout = await payouts.Tracked(new Specification<OwnerPayout>(x => x.BillId == n.BillId && x.Status == OwnerPayoutStatus.Posted)).FirstOrDefaultAsync(ct);
        if (payout is null) return;
        await ReleaseAsync(payout, ct);
        VoidMemo.Stamp(payout, "The owner's bill was voided", clock, user);
        await payouts.SaveChangesAsync(ct);
    }

    private async Task ReleaseAsync(OwnerPayout payout, CancellationToken ct)
    {
        payout.Status = OwnerPayoutStatus.Voided;
        if (payout.JournalEntryId is not { } id) return;
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == id).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseTodayAsync(journal, $"Void owner payout {payout.Number}", ct);
    }

    private static OwnerPayoutDto ToDto(OwnerPayout p) => new(p.Id, p.Number, p.LegalEntityId, p.OwnerPartyId, p.PeriodFrom, p.PeriodTo, p.Date, p.Method, p.Status, p.RentCollected, p.ManagementFee, p.MaintenanceCosts, p.Amount, p.BankAccountId, p.BillId, p.JournalEntryId, p.Notes, p.VoidReason, p.VoidedAt);
}
