using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>My own employee record, pay included.</summary>
public sealed record GetMyEmployeeQuery() : IQuery<EmployeeDto>;
