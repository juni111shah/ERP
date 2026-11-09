using System.Text.Json.Nodes;

namespace ERP.Modules.RealEstate.Features;

public sealed record LeaseInput(
    Guid UnitId, Guid LesseePartyId, DateOnly StartDate, DateOnly? EndDate, decimal AnnualRent, int PaymentFrequency = 4,
    decimal SecurityDepositAmount = 0, Guid? TaxCodeId = null, int GraceDays = 0, int NoticePeriodDays = 90, string? Notes = null,
    JsonObject? CustomFields = null, bool ActivateNow = false);

public sealed record RenewalInput(DateOnly? EndDate, decimal? AnnualRent, int? PaymentFrequency, decimal? SecurityDepositAmount, string? Notes);
