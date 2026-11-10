using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetChequeByIdHandler(IPaymentInstrumentRepository instruments, ChequeDtoBuilder dtos) : IQueryHandler<GetChequeByIdQuery, ChequeDto>
{
    public async Task<ChequeDto> Handle(GetChequeByIdQuery query, CancellationToken ct)
    {
        var i = await instruments.GetAsync(query.Id, ct);
        return await dtos.BuildAsync(i, ct);
    }
}
