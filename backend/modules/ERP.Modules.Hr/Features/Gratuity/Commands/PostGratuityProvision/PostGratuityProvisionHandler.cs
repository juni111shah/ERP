using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;

namespace ERP.Modules.Hr.Features;

internal sealed class PostGratuityProvisionHandler(GratuityLiability liabilities, GratuityProvision provisions, ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<PostGratuityProvisionCommand, GratuityProvisionDto>
{
    public async Task<GratuityProvisionDto> Handle(PostGratuityProvisionCommand command, CancellationToken ct)
    {
        // 1. The liability and what the provision account holds now.
        var cmd = command;
        var entity = await guard.RequireAsync(cmd.LegalEntityId, ct);
        var (rows, _) = await liabilities.BuildAsync(cmd.AsOf, entity.Id, ct);
        var liability = rows.Sum(r => r.Gratuity);
        var provisionAccount = await posting.AccountForRoleAsync("payroll.gratuity.provision", ct);
        var provision = await provisions.BalanceOfAsync(provisionAccount, entity.Id, cmd.AsOf, ct);
        var diff = liability - provision;
        if (diff == 0) return new GratuityProvisionDto(cmd.AsOf, liability, provision, 0, null);

        // 2. Post the difference and save.
        var expense = await posting.AccountForRoleAsync("payroll.gratuity.expense", ct);
        var memo = $"End-of-service provision true-up {cmd.AsOf:yyyy-MM}";
        var amount = Math.Abs(diff);
        var lines = diff > 0 ? new[] { new PostingLine(expense, amount, 0, memo), new PostingLine(provisionAccount, 0, amount, memo) } : new[] { new PostingLine(provisionAccount, amount, 0, memo), new PostingLine(expense, 0, amount, memo) };
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, cmd.AsOf, "gratuity", null, memo, null, lines), ct);
        await uow.SaveChangesAsync(ct);
        return new GratuityProvisionDto(cmd.AsOf, liability, provision + diff, diff, journal.Id);
    }
}
