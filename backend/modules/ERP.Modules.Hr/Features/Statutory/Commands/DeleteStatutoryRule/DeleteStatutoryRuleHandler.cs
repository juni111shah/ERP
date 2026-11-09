using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteStatutoryRuleHandler(IStatutoryRuleRepository rules, IRepository<StatutorySlab> slabs, IReadRepository<PayrollLineStatutory> lines) : ICommandHandler<DeleteStatutoryRuleCommand>
{
    public async Task<Unit> Handle(DeleteStatutoryRuleCommand command, CancellationToken ct)
    {
        // 1. A rule used in payroll stays.
        var rule = await rules.GetWithSlabsForUpdateAsync(command.Id, ct);
        if (await lines.AnyAsync(l => l.RuleId == rule.Id, ct)) throw new ConflictException("This rule was used in payroll. Switch it off instead of deleting it.");

        // 2. Remove it with its slabs, and save.
        foreach (var s in rule.Slabs.ToList()) slabs.Remove(s);
        rules.Remove(rule);
        await rules.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
