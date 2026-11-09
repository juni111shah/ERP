using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class GetPayslipHandler(PayslipDocuments payslips) : IQueryHandler<GetPayslipQuery, PayslipDto>
{
    public async Task<PayslipDto> Handle(GetPayslipQuery query, CancellationToken ct)
    {
        var run = await payslips.LoadRunAsync(query.RunId, ct);
        return await payslips.BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == query.LineId) ?? throw new NotFoundException(), ct);
    }
}
