using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>Converts a lead into a customer and an opportunity; returns the opportunity's id.</summary>
public record ConvertLeadCommand(
    Guid Id,
    string OpportunityTitle,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate) : ICommand<Guid>;
