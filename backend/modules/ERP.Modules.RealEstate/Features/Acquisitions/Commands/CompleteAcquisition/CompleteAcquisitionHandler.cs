using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CompleteAcquisitionHandler(
    IRepository<Acquisition> acquisitions, IFundingSourceRepository sources, IRepository<FundingInstalment> instalments, IReadRepository<Account> accounts,
    AcquisitionDtoFactory factory, IPostingService posting, IUnitOfWork uow, IClock clock)
    : ICommandHandler<CompleteAcquisitionCommand, AcquisitionDto>
{
    public Task<AcquisitionDto> Handle(CompleteAcquisitionCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a fully funded draft, with a bank account for the cash part.
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, token);
        if (a.Status != AcquisitionStatus.Draft) throw new ConflictException($"Only a draft acquisition can be completed (this one is {a.Status}).");
        var funding = await sources.ListOfAcquisitionByDueForUpdateAsync(a.Id, token);
        var financed = funding.Sum(f => f.Principal);
        var unfunded = a.PurchasePrice - a.DownPayment - financed;
        if (unfunded != 0) throw new ConflictException($"The purchase is not fully funded: {unfunded:0.00} of the price is covered by neither the down payment nor a funding source.");
        if (a.CashAtCompletion > 0 && a.PayFromAccountId is null) throw new FieldValidationException("payFromAccountId", "Choose the bank account the down payment and fees are paid from.");

        // 2. Post Dr investment property, Cr bank and financing payable.
        var property = await posting.AccountForRoleAsync("realestate.investment", token);
        var memo = $"Acquisition {a.Number}";
        var lines = new List<PostingLine> { new(property, a.TotalCost, 0, memo) };
        if (a.CashAtCompletion > 0)
        {
            var bank = await accounts.FindAsync(a.PayFromAccountId!.Value, token);
            if (bank is null || !ERP.Application.Finance.BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("payFromAccountId", "Choose an active bank account.");
            lines.Add(new PostingLine(bank.Id, 0, a.CashAtCompletion, memo));
        }
        if (financed > 0)
        {
            var payable = await posting.AccountForRoleAsync("realestate.financing", token);
            foreach (var f in funding) lines.Add(new PostingLine(payable, 0, f.Principal, $"{f.Lender} {memo}"));
        }
        var journal = await posting.PostAsync(new PostingRequest(a.LegalEntityId, a.PurchaseDate, "acquisition", a.Id, memo, a.ContractReference ?? a.Number, lines, a.CurrencyCode), token);
        a.Complete(journal.Id, clock.UtcNow);

        // 3. Generate the instalment schedule of each funding source; save.
        foreach (var f in funding)
            instalments.AddRange(FundingSchedule.Generate(f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate).Select(p => new FundingInstalment
            {
                LegalEntityId = f.LegalEntityId, FundingSourceId = f.Id, Sequence = p.Sequence, DueDate = p.DueDate, Principal = p.Principal, Interest = p.Interest, Total = p.Total, BalanceAfter = p.BalanceAfter,
            }));
        await acquisitions.SaveChangesAsync(token);
        return await factory.BuildAsync(a, token);
    }, ct);
}
