using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetInspectionHandler(IInspectionRepository inspections, InspectionDtoFactory factory) : IQueryHandler<GetInspectionQuery, InspectionDto>
{
    public async Task<InspectionDto> Handle(GetInspectionQuery q, CancellationToken ct) => await factory.BuildAsync(await inspections.GetWithItemsAsync(q.Id, ct), ct);
}
