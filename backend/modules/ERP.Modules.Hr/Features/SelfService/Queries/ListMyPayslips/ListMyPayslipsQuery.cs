using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>My payslips of posted and paid runs, newest first.</summary>
public sealed record ListMyPayslipsQuery() : IQuery<IReadOnlyList<MyPayslipDto>>;
