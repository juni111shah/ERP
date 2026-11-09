using ERP.Application.Common;
using ERP.Application.Printing;

namespace ERP.Modules.Hr.Features;

internal sealed class GetPayslipPdfHandler(PayslipDocuments payslips, IPrintPdfRenderer renderer) : IQueryHandler<GetPayslipPdfQuery, PrintPdfFile>
{
    public async Task<PrintPdfFile> Handle(GetPayslipPdfQuery query, CancellationToken ct)
    {
        var run = await payslips.LoadRunAsync(query.RunId, ct);
        var p = await payslips.BuildAsync(run, run.Lines.FirstOrDefault(l => l.Id == query.LineId) ?? throw new NotFoundException(), ct);
        return new(renderer.Render(await payslips.ToDocumentAsync(run, p, ct)), PrintPdfNames.FileName("Payslip", $"{p.EmployeeNumber}-{p.Year}{p.Month:00}", p.LineId));
    }
}
