using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The deposit register of a lease and the bank check its movements need (shared by the deposit handlers).</summary>
internal sealed class SecurityDepositLedger(IDepositTransactionRepository transactions, IReadRepository<Account> accounts)
{
    public const string Role = "realestate.deposits";

    public async Task<DepositDto> BuildAsync(Lease lease, CancellationToken ct)
    {
        var rows = await transactions.ListOfLeaseAsync(lease.Id, ct);
        var received = rows.Where(r => r.Kind is DepositKind.Received or DepositKind.TransferIn).Sum(r => r.Amount);
        var applied = rows.Where(r => r.Kind is DepositKind.Damages or DepositKind.Arrears or DepositKind.Refund or DepositKind.TransferOut).Sum(r => r.Amount);
        return new DepositDto(lease.Id, lease.SecurityDepositAmount, received, applied, received - applied, rows.Select(r => new DepositTransactionDto(r.Id, r.Date, r.Kind, r.Amount, r.Description)).ToList());
    }

    public async Task RequireBankAsync(Guid id, string field, CancellationToken ct)
    {
        var a = await accounts.FindAsync(id, ct);
        if (a is null || !BankAccountRules.IsCashOrBank(a)) throw new FieldValidationException(field, "Choose an active bank or cash account.");
    }
}
