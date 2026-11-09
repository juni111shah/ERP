using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class RegisterChequeHandler(IPaymentInstrumentRepository instruments, ChequeRegistration registration, IUnitOfWork uow, ChequeDtoBuilder dtos)
    : ICommandHandler<RegisterChequeCommand, ChequeDto>
{
    public Task<ChequeDto> Handle(RegisterChequeCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var cheque = await registration.RegisterAsync(command.Input, null, token);
        await instruments.SaveChangesAsync(token);
        return await dtos.BuildAsync(cheque, token);
    }, ct);
}
