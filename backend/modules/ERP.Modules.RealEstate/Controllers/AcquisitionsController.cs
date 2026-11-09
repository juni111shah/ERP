using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Property acquisitions, their funding sources and financing instalments.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/acquisitions")]
public sealed class AcquisitionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.AcquisitionsRead)]
    public Task<PagedResult<AcquisitionSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, AcquisitionSummaryDto.Projection, P.AcquisitionsRead, ct);

    [HttpGet("overview")]
    [RequirePermission(P.AcquisitionsRead)]
    public Task<AcquisitionOverviewDto> GetOverview(CancellationToken ct)
        => Mediator.Send(new GetAcquisitionOverviewQuery(), ct);

    [HttpGet("funding")]
    [RequirePermission(P.AcquisitionsRead)]
    public Task<IReadOnlyList<FundingSourceDto>> ListFunding(CancellationToken ct)
        => Mediator.Send(new ListFundingSourcesQuery(), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.AcquisitionsRead)]
    public Task<AcquisitionDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAcquisitionQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.AcquisitionsManage)]
    public async Task<IActionResult> Create(AcquisitionInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateAcquisitionCommand(input), ct);
        return Created($"/api/v1/m/realestate/acquisitions/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.AcquisitionsManage)]
    public Task<AcquisitionDto> Update(Guid id, AcquisitionInput input, CancellationToken ct)
        => Mediator.Send(new UpdateAcquisitionCommand(id, input), ct);

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(P.AcquisitionsManage)]
    public Task<AcquisitionDto> Cancel(Guid id, CancellationToken ct)
        => Mediator.Send(new CancelAcquisitionCommand(id), ct);

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(P.AcquisitionsComplete)]
    public Task<AcquisitionDto> Complete(Guid id, CancellationToken ct)
        => Mediator.Send(new CompleteAcquisitionCommand(id), ct);

    [HttpPost("instalments/{id:guid}/pay")]
    [RequirePermission(P.FundingPay)]
    public Task<FundingSourceDto> PayInstalment(Guid id, PayInstalmentRequest b, CancellationToken ct)
        => Mediator.Send(new PayFundingInstalmentCommand(id, b.BankAccountId, b.Date), ct);

    public sealed record PayInstalmentRequest(Guid BankAccountId, DateOnly Date);
}
