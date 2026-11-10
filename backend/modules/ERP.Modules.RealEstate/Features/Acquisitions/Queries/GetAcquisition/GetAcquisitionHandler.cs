using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetAcquisitionHandler(IReadRepository<Acquisition> acquisitions, AcquisitionDtoFactory factory) : IQueryHandler<GetAcquisitionQuery, AcquisitionDto>
{
    public async Task<AcquisitionDto> Handle(GetAcquisitionQuery q, CancellationToken ct) => await factory.BuildAsync(await acquisitions.GetAsync(q.Id, ct), ct);
}
