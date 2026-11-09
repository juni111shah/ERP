using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>What an employee would be owed when leaving on the given date (nothing is saved).</summary>
public sealed record GetSettlementPreviewQuery(Guid EmployeeId, DateOnly TerminationDate, DateOnly? NoticeGivenOn) : IQuery<SettlementPreviewDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
