using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateStatutoryRuleHandler(IStatutoryRuleRepository rules, IRepository<StatutorySlab> slabs) : ICommandHandler<UpdateStatutoryRuleCommand, StatutoryRuleDto>
{
    public async Task<StatutoryRuleDto> Handle(UpdateStatutoryRuleCommand command, CancellationToken ct)
    {
        // 1. Load it with its slabs; the entity stays, a changed code must still be unique.
        var i = command.Input;
        var rule = await rules.GetWithSlabsForUpdateAsync(command.Id, ct);
        if (i.LegalEntityId != rule.LegalEntityId) throw new FieldValidationException("legalEntityId", "A rule cannot move to another entity.");
        var code = i.Code.Trim().ToUpperInvariant();
        if (code != rule.Code && await rules.AnyAsync(r => r.LegalEntityId == rule.LegalEntityId && r.Code == code && r.Id != rule.Id, ct)) throw new ConflictException($"A rule with code '{code}' already exists for this entity.");

        // 2. Fill, replace the slabs, and save.
        StatutoryRuleMapping.Fill(rule, i, code);
        foreach (var old in rule.Slabs.ToList()) slabs.Remove(old);
        rule.Slabs.Clear();
        StatutoryRuleMapping.AddSlabs(slabs, rule, i);
        await rules.SaveChangesAsync(ct);
        return StatutoryRuleDto.From(rule);
    }
}
