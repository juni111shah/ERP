using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Counter sales over a range of shop days (today when no dates are given), by day, hour, tender, register and cashier.</summary>
public record GetPosSalesReportQuery(DateOnly? From, DateOnly? To, Guid? RegisterId, Guid? LegalEntityId) : IQuery<PosSalesReportDto>;
