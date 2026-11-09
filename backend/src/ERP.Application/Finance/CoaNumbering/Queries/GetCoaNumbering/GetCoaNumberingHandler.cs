using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetCoaNumberingHandler(AccountNumbering numbering) : IQueryHandler<GetCoaNumberingQuery, CoaNumberingDto>
{
    public async Task<CoaNumberingDto> Handle(GetCoaNumberingQuery q, CancellationToken ct) => CoaNumberingDto.From(await numbering.GetMaskAsync(ct));
}
