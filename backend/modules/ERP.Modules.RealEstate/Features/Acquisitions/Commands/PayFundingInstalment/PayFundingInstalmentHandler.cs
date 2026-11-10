using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class PayFundingInstalmentHandler(
    IRepository<FundingInstalment> instalments, IReadRepository<FundingSource> sources, IReadRepository<Account> accounts, AcquisitionDtoFactory factory, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<PayFundingInstalmentCommand, FundingSourceDto>
{
    public Task<FundingSourceDto> Handle(PayFundingInstalmentCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. An unpaid instalment, the earlier ones paid, from a bank account.
        var inst = await instalments.GetForUpdateAsync(cmd.InstalmentId, token);
        if (inst.Status == InstalmentPayStatus.Paid) throw new ConflictException("This instalment is already paid.");
        var earlier = await instalments.AnyAsync(i => i.FundingSourceId == inst.FundingSourceId && i.Sequence < inst.Sequence && i.Status == InstalmentPayStatus.Scheduled, token);
        if (earlier) throw new ConflictException("Pay the earlier instalments first.");
        var source = await sources.GetAsync(inst.FundingSourceId, token);
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || !ERP.Application.Finance.BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");

        // 2. Post Dr financing payable and finance costs, Cr bank.
        var count = await instalments.CountAsync(i => i.FundingSourceId == source.Id, token);
        var memo = $"{source.Lender} instalment {inst.Sequence}/{count}";
        var lines = new List<PostingLine> { new(await posting.AccountForRoleAsync("realestate.financing", token), inst.Principal, 0, memo) };
        if (inst.Interest > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("realestate.expense.finance", token), inst.Interest, 0, memo));
        lines.Add(new PostingLine(bank.Id, 0, inst.Total, memo));
        var journal = await posting.PostAsync(new PostingRequest(inst.LegalEntityId, cmd.Date, "financing", inst.Id, memo, source.Lender, lines), token);

        // 3. The instalment is paid; save.
        inst.Status = InstalmentPayStatus.Paid; inst.PaidOn = cmd.Date; inst.JournalEntryId = journal.Id;
        await instalments.SaveChangesAsync(token);
        return await factory.BuildSourceAsync(source, includeInstalments: true, token);
    }, ct);
}
