using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateStatutoryRuleHandler(IStatutoryRuleRepository rules, IRepository<StatutorySlab> slabs, ILegalEntityGuard guard) : ICommandHandler<CreateStatutoryRuleCommand, StatutoryRuleDto>
{
    public async Task<StatutoryRuleDto> Handle(CreateStatutoryRuleCommand command, CancellationToken ct)
    {
        // 1. The code is unique within the entity.
        var i = command.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var code = i.Code.Trim().ToUpperInvariant();
        if (await rules.AnyAsync(r => r.LegalEntityId == entity.Id && r.Code == code, ct)) throw new ConflictException($"A rule with code '{code}' already exists for this entity.");

        // 2. Fill, add with the slabs, and save.
        var rule = new StatutoryRule { LegalEntityId = entity.Id };
        StatutoryRuleMapping.Fill(rule, i, code);
        rules.Add(rule);
        StatutoryRuleMapping.AddSlabs(slabs, rule, i);
        await rules.SaveChangesAsync(ct);
        return StatutoryRuleDto.From(rule);
    }
}
