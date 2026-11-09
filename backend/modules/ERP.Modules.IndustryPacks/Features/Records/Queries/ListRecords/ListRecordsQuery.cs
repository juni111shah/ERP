using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>A page of the records of one type.</summary>
public sealed record ListRecordsQuery(string Module, string Type, PagedQuery Paging) : RecordRequest(Module, Type), IQuery<PagedResult<RecordDto>>
{
    public override string RequiredPermission => PackModule.ReadPermission(Module, Type);
}
