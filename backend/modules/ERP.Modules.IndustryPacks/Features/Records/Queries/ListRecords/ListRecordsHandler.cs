using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class ListRecordsHandler(IIndustryRecordRepository records, RecordDtoBuilder dtos) : IQueryHandler<ListRecordsQuery, PagedResult<RecordDto>>
{
    public async Task<PagedResult<RecordDto>> Handle(ListRecordsQuery q, CancellationToken ct)
    {
        var def = PackCatalog.Require(q.Module, q.Type);
        var page = await records.PageOfTypeAsync(q.Module, q.Type, q.Paging, ct);
        return page.Map(r => dtos.ToDto(r, def, []));
    }
}
