using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record FundingInstalmentDto(Guid Id, int Sequence, DateOnly DueDate, decimal Principal, decimal Interest, decimal Total, decimal BalanceAfter, InstalmentPayStatus Status, DateOnly? PaidOn);
public sealed record FundingSourceDto(
    Guid Id, Guid AcquisitionId, FundingType Type, string Lender, decimal Principal, decimal AnnualRatePercent, int TermMonths, DateOnly FirstDueDate, decimal MonthlyPayment, decimal FinanceCost,
    int Paid, int InstalmentCount, decimal Outstanding, DateOnly? NextDue, IReadOnlyList<FundingInstalmentDto> Instalments);
public sealed record AcquisitionDto(
    Guid Id, string? Number, Guid LegalEntityId, Guid PropertyId, string? PropertyName, string SellerName, DateOnly PurchaseDate, string? ContractReference, decimal PurchasePrice, decimal DownPayment,
    decimal TransferFees, decimal OtherCosts, Guid? PayFromAccountId, string? Notes, string CurrencyCode, AcquisitionStatus Status, decimal TotalCost, decimal CashAtCompletion, decimal Financed, decimal Unfunded,
    Guid? JournalEntryId, IReadOnlyList<FundingSourceDto> Funding);

public sealed record AcquisitionSummaryDto(Guid Id, string? Number, Guid PropertyId, string SellerName, DateOnly PurchaseDate, decimal PurchasePrice, decimal DownPayment, AcquisitionStatus Status, string CurrencyCode)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Acquisition, AcquisitionSummaryDto>> Projection = a => new AcquisitionSummaryDto(a.Id, a.Number, a.PropertyId, a.SellerName, a.PurchaseDate, a.PurchasePrice, a.DownPayment, a.Status, a.CurrencyCode);
}

public sealed record AcquisitionOverviewDto(decimal Consideration, int Count, decimal DownPayments, decimal Financing, decimal Outstanding, decimal Unfunded);
