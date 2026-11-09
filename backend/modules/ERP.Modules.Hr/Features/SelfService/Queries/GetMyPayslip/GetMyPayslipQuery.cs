using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>One of my payslips.</summary>
public sealed record GetMyPayslipQuery(Guid LineId) : IQuery<MyPayslipDto>;
