using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class DepositChequeHandler(IPaymentInstrumentRepository instruments, IReadRepository<Account> accounts, ChequeDtoBuilder dtos)
    : ICommandHandler<DepositChequeCommand, ChequeDto>
{
    public async Task<ChequeDto> Handle(DepositChequeCommand command, CancellationToken ct)
    {
        // 1. Only a cheque in custody, not before the date written on it, into an active bank account.
        var c = await instruments.GetForUpdateAsync(command.Id, ct);
        if (c.Status != InstrumentStatus.InCustody) throw new ConflictException("Only a cheque in custody can be deposited.");
        if (c.InstrumentDate > command.Date) throw new ConflictException($"This cheque is post-dated to {c.InstrumentDate:yyyy-MM-dd}; it cannot be deposited before then.");
        await RequireBankAsync(command.BankAccountId, ct);

        // 2. Mark it deposited and save.
        c.BankAccountId = command.BankAccountId; c.DepositedOn = command.Date; c.Status = InstrumentStatus.Deposited;
        await instruments.SaveChangesAsync(ct);
        return await dtos.BuildAsync(c, ct);
    }

    private async Task RequireBankAsync(Guid accountId, CancellationToken ct)
    {
        var a = await accounts.FindAsync(accountId, ct);
        if (a is null || !BankAccountRules.IsBankOnly(a)) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
    }
}
