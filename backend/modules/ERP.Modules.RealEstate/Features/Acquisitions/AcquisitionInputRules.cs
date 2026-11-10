using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks the property of an acquisition form and copies the form onto the acquisition and its funding (shared by create and update).</summary>
internal sealed class AcquisitionInputRules(IReadRepository<Property> properties)
{
    public async Task RequirePropertyAsync(AcquisitionInput i, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (property.LegalEntityId != i.LegalEntityId) throw new FieldValidationException("propertyId", "The property belongs to another legal entity.");
    }

    public static void Map(AcquisitionInput i, Acquisition a)
    {
        a.PropertyId = i.PropertyId; a.SellerName = i.SellerName.Trim(); a.PurchaseDate = i.PurchaseDate; a.ContractReference = i.ContractReference?.Trim(); a.PurchasePrice = i.PurchasePrice;
        a.DownPayment = i.DownPayment; a.TransferFees = i.TransferFees; a.OtherCosts = i.OtherCosts; a.PayFromAccountId = i.PayFromAccountId; a.Notes = i.Notes;
    }

    public static FundingSource NewSource(FundingInput f, Acquisition a) => new()
    {
        LegalEntityId = a.LegalEntityId, AcquisitionId = a.Id, Type = f.Type, Lender = f.Lender.Trim(), Principal = f.Principal, AnnualRatePercent = f.AnnualRatePercent, TermMonths = f.TermMonths,
        FirstDueDate = f.FirstDueDate, MonthlyPayment = FundingSchedule.MonthlyPayment(f.Principal, f.AnnualRatePercent, f.TermMonths),
    };
}
