using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetStatutoryRuleHandler(IStatutoryRuleRepository rules) : IQueryHandler<GetStatutoryRuleQuery, StatutoryRuleDto>
{
    public async Task<StatutoryRuleDto> Handle(GetStatutoryRuleQuery query, CancellationToken ct) => StatutoryRuleDto.From(await rules.GetWithSlabsAsync(query.Id, ct));
}
