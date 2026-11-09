using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Builds acquisition and funding source DTOs with their schedules (shared by the acquisition handlers).</summary>
internal sealed class AcquisitionDtoFactory(IFundingSourceRepository sources, IFundingInstalmentRepository instalments, IPropertyRepository properties)
{
    public async Task<AcquisitionDto> BuildAsync(Acquisition a, CancellationToken ct)
    {
        var funding = await sources.ListOfAcquisitionAsync(a.Id, ct);
        var dtos = new List<FundingSourceDto>();
        foreach (var f in funding) dtos.Add(await BuildSourceAsync(f, includeInstalments: true, ct));
        var propertyName = await properties.FindNameAsync(a.PropertyId, ct);
        var financed = funding.Sum(f => f.Principal);
        return new AcquisitionDto(a.Id, a.Number, a.LegalEntityId, a.PropertyId, propertyName, a.SellerName, a.PurchaseDate, a.ContractReference, a.PurchasePrice, a.DownPayment, a.TransferFees, a.OtherCosts,
            a.PayFromAccountId, a.Notes, a.CurrencyCode, a.Status, a.TotalCost, a.CashAtCompletion, financed, a.PurchasePrice - a.DownPayment - financed, a.JournalEntryId, dtos);
    }

    public async Task<FundingSourceDto> BuildSourceAsync(FundingSource f, bool includeInstalments, CancellationToken ct)
    {
        var rows = await instalments.ListOfSourceAsync(f.Id, ct);
        var paid = rows.Where(r => r.Status == InstalmentPayStatus.Paid).ToList();
        var next = rows.FirstOrDefault(r => r.Status == InstalmentPayStatus.Scheduled);
        var outstanding = rows.Count == 0 ? f.Principal : f.Principal - paid.Sum(r => r.Principal);
        var cost = rows.Count == 0 ? FundingSchedule.Generate(f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate).Sum(p => p.Interest) : rows.Sum(r => r.Interest);
        return new FundingSourceDto(f.Id, f.AcquisitionId, f.Type, f.Lender, f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate, f.MonthlyPayment, cost, paid.Count, f.TermMonths, outstanding, next?.DueDate,
            includeInstalments ? rows.Select(r => new FundingInstalmentDto(r.Id, r.Sequence, r.DueDate, r.Principal, r.Interest, r.Total, r.BalanceAfter, r.Status, r.PaidOn)).ToList() : []);
    }
}
