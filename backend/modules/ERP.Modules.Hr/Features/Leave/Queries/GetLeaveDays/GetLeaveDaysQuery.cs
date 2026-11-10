using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>How many working days (weekends and public holidays left out) a period has.</summary>
public sealed record GetLeaveDaysQuery(DateOnly From, DateOnly To) : IQuery<LeaveDaysDto>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
