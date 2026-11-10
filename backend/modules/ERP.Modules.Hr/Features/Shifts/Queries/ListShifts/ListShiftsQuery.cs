using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Every shift, by code, with how many employees are on it.</summary>
public sealed record ListShiftsQuery() : IQuery<IReadOnlyList<ShiftDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
