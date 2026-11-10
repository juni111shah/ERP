using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosReturnByIdHandler(IPosReturnRepository returns) : IQueryHandler<GetPosReturnByIdQuery, PosReturnDto>
{
    public async Task<PosReturnDto> Handle(GetPosReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var ret = await returns.FindWithLinesAndRefundsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Return {request.Id} not found");
        return PosReturnDto.From(ret);
    }
}
