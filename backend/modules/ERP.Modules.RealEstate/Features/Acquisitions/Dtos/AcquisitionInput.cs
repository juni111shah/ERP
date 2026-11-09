using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record FundingInput(FundingType Type, string Lender, decimal Principal, decimal AnnualRatePercent, int TermMonths, DateOnly FirstDueDate);
public sealed record AcquisitionInput(
    Guid LegalEntityId, Guid PropertyId, string SellerName, DateOnly PurchaseDate, string? ContractReference, decimal PurchasePrice, decimal DownPayment, decimal TransferFees, decimal OtherCosts,
    Guid? PayFromAccountId, string? Notes, List<FundingInput>? Funding);
