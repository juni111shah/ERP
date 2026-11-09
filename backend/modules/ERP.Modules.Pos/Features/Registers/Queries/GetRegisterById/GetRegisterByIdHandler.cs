using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

internal sealed class GetRegisterByIdHandler(IRepository<PosRegister> registers) : IQueryHandler<GetRegisterByIdQuery, PosRegisterDto?>
{
    public async Task<PosRegisterDto?> Handle(GetRegisterByIdQuery request, CancellationToken cancellationToken)
    {
        var reg = await registers.FindAsync(request.Id, cancellationToken);
        return reg is null ? null : PosRegisterDto.From(reg);
    }
}
