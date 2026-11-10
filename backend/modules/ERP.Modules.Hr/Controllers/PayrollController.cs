using ERP.Application.Common;
using ERP.Application.Printing;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Payroll runs: generate, adjust, post, pay and void; the bank and WPS files, payslips and the statutory totals.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/payroll")]
public sealed class PayrollController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.PayrollRead)]
    public Task<PagedResult<PayrollSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PayrollSummaryDto.Projection, P.PayrollRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.PayrollRead)]
    public Task<PayrollDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetPayrollQuery(id), ct);

    [HttpGet("{id:guid}/bank-file")]
    [RequirePermission(P.PayrollPay)]
    public async Task<IActionResult> BankFile(Guid id, bool? unpaidOnly, CancellationToken ct)
        => Content(await Mediator.Send(new GetPayrollBankFileQuery(id, unpaidOnly == true), ct), "text/csv");

    [HttpGet("{id:guid}/wps-sif")]
    [RequirePermission(P.PayrollPay)]
    public async Task<IActionResult> WpsFile(Guid id, bool? unpaidOnly, CancellationToken ct)
        => Content(await Mediator.Send(new GetPayrollWpsFileQuery(id, unpaidOnly == true), ct), "text/csv");

    [HttpGet("{id:guid}/payslips/{lineId:guid}")]
    [RequirePermission(P.PayrollRead)]
    public Task<PayslipDto> Payslip(Guid id, Guid lineId, CancellationToken ct) => Mediator.Send(new GetPayslipQuery(id, lineId), ct);

    [HttpGet("{id:guid}/payslips/{lineId:guid}/payslip.pdf")]
    [RequirePermission(P.PayrollRead)]
    public async Task<IActionResult> PayslipPdf(Guid id, Guid lineId, CancellationToken ct)
    {
        var f = await Mediator.Send(new GetPayslipPdfQuery(id, lineId), ct);
        return File(f.Content, PrintPdfFile.ContentType, f.FileName);
    }

    [HttpPost("{id:guid}/payslips/{lineId:guid}/email")]
    [RequirePermission(P.PayrollRead)]
    public Task<EmailSentDto> EmailPayslip(Guid id, Guid lineId, PayslipEmailInput? body, CancellationToken ct)
        => Mediator.Send(new EmailPayslipCommand(id, lineId, body ?? new(null, null)), ct);

    [HttpPost("{id:guid}/email-payslips")]
    [RequirePermission(P.PayrollRead)]
    public Task<PayslipBulkEmailResult> EmailPayslips(Guid id, CancellationToken ct) => Mediator.Send(new EmailPayslipsCommand(id), ct);

    [HttpPost]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> Generate(GeneratePayrollInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new GeneratePayrollCommand(input), ct);
        return Created($"{Base}/payroll/{created.Id}", created);
    }

    [HttpPost("{id:guid}/recalculate")]
    [RequirePermission(P.PayrollManage)]
    public Task<PayrollDto> Recalculate(Guid id, CancellationToken ct) => Mediator.Send(new RecalculatePayrollCommand(id), ct);

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [RequirePermission(P.PayrollManage)]
    public Task<PayrollDto> AdjustLine(Guid id, Guid lineId, AdjustLineRequest b, CancellationToken ct) => Mediator.Send(new AdjustPayrollLineCommand(id, lineId, b.Adjustment, b.Note), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePayrollCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission(P.PayrollPost)]
    public Task<PayrollDto> Post(Guid id, CancellationToken ct) => Mediator.Send(new PostPayrollCommand(id), ct);

    [HttpPost("{id:guid}/pay")]
    [RequirePermission(P.PayrollPay)]
    public Task<PayrollDto> Pay(Guid id, PayrollPayRequest b, CancellationToken ct) => Mediator.Send(new PayPayrollCommand(id, b.BankAccountId, b.Date, b.EmployeeIds), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(P.PayrollPost)]
    public Task<PayrollDto> Void(Guid id, PayrollVoidRequest b, CancellationToken ct) => Mediator.Send(new VoidPayrollCommand(id, b.Reason), ct);

    [HttpGet("{id:guid}/statutory")]
    [RequirePermission(P.PayrollRead)]
    public Task<IReadOnlyList<StatutoryTotalDto>> Statutory(Guid id, CancellationToken ct) => Mediator.Send(new GetPayrollStatutoryQuery(id), ct);

    /// <summary>The manual adjustment of one line and why.</summary>
    public sealed record AdjustLineRequest(decimal Adjustment, string? Note);

    /// <summary>The bank account and date of the payment; only the given employees when any are listed.</summary>
    public sealed record PayrollPayRequest(Guid BankAccountId, DateOnly Date, IReadOnlyList<Guid>? EmployeeIds = null);

    /// <summary>Why the run is voided (required).</summary>
    public sealed record PayrollVoidRequest(string? Reason);
}
