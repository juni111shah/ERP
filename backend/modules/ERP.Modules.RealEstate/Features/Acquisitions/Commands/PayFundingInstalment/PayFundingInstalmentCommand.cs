using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Pays one financing instalment: Dr financing payable (principal) and finance costs (interest), Cr bank. Instalments are paid in order.</summary>
public sealed record PayFundingInstalmentCommand(Guid InstalmentId, Guid BankAccountId, DateOnly Date) : ICommand<FundingSourceDto>, IRequirePermission { public string RequiredPermission => P.FundingPay; }
