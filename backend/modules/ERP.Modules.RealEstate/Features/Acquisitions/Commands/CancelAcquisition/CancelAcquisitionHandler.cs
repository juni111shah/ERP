using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CancelAcquisitionHandler(IRepository<Acquisition> acquisitions, AcquisitionDtoFactory factory) : ICommandHandler<CancelAcquisitionCommand, AcquisitionDto>
{
    public async Task<AcquisitionDto> Handle(CancelAcquisitionCommand cmd, CancellationToken ct)
    {
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, ct);
        a.Cancel();
        await acquisitions.SaveChangesAsync(ct);
        return await factory.BuildAsync(a, ct);
    }
}
