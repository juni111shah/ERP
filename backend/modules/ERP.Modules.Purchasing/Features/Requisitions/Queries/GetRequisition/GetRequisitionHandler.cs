using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class GetRequisitionHandler(IPurchaseRequisitionRepository requisitions, ICustomFieldService fields) : IQueryHandler<GetRequisitionQuery, RequisitionDto>
{
    public async Task<RequisitionDto> Handle(GetRequisitionQuery q, CancellationToken ct)
    {
        var req = await requisitions.FindWithLinesForUpdateAsync(q.Id, ct) ?? throw new NotFoundException($"Requisition '{q.Id}' not found.");
        return await RequisitionDto.FromAsync(req, fields, ct);
    }
}
