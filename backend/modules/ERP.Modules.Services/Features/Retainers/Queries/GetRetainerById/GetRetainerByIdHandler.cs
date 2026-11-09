using ERP.Application.Common;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class GetRetainerByIdHandler(IRetainerContractRepository retainers) : IQueryHandler<GetRetainerByIdQuery, RetainerDto?>
{
    public async Task<RetainerDto?> Handle(GetRetainerByIdQuery request, CancellationToken cancellationToken)
    {
        var contract = await retainers.FindAsync(request.Id, cancellationToken);
        return contract is null ? null : RetainerDto.From(contract);
    }
}
