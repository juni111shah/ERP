using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class PayDealAgentHandler(IRepository<BrokerageDeal> deals, IReadRepository<Account> accounts, IPostingService posting, ICustomFieldService fields, IUnitOfWork uow)
    : ICommandHandler<PayDealAgentCommand, DealDto>
{
    public Task<DealDto> Handle(PayDealAgentCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a collected deal, with a share to pay, from a bank account.
        var deal = await deals.GetForUpdateAsync(cmd.Id, token);
        if (deal.Stage != DealStage.Collected) throw new ConflictException("The agent can be paid only after the client has paid the commission invoice.");
        decimal Share(decimal pct) => Math.Round(deal.GrossCommission * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var payout = Share(deal.AgentSharePercent) + Share(deal.TeamLeadSharePercent);
        if (payout <= 0) throw new ConflictException("This deal has no agent or team lead share to pay.");
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || !ERP.Application.Finance.BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");

        // 2. Post Dr commission expense, Cr bank.
        var expense = await posting.AccountForRoleAsync("realestate.expense.commission", token);
        var memo = $"Commission payout {deal.Number}";
        var journal = await posting.PostAsync(new PostingRequest(deal.LegalEntityId, cmd.Date, DealCommission.SourceType, deal.Id, memo, deal.Number,
            [new PostingLine(expense, payout, 0, memo), new PostingLine(bank.Id, 0, payout, memo)], deal.CurrencyCode), token);

        // 3. The agent is paid; save.
        deal.MarkAgentPaid(journal.Id, cmd.Date);
        await deals.SaveChangesAsync(token);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, token));
    }, ct);
}
