using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetChecklistTemplateHandler(IChecklistTemplateRepository templates) : IQueryHandler<GetChecklistTemplateQuery, ChecklistTemplateDto>
{
    public async Task<ChecklistTemplateDto> Handle(GetChecklistTemplateQuery query, CancellationToken ct) => ChecklistTemplateDto.From(await templates.GetWithItemsAsync(query.Id, ct));
}
