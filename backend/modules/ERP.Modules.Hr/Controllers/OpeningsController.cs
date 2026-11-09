using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Recruitment: vacancies.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/openings")]
public sealed class OpeningsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.RecruitmentRead)]
    public Task<PagedResult<JobOpeningRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, JobOpeningRow.Projection, P.RecruitmentRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.RecruitmentRead)]
    public Task<JobOpeningDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetJobOpeningQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.RecruitmentManage)]
    public async Task<IActionResult> Create(JobOpeningInput input, CancellationToken ct)
    {
        var o = await Mediator.Send(new CreateJobOpeningCommand(input), ct);
        return Created($"{Base}/openings/{o.Id}", o);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.RecruitmentManage)]
    public Task<JobOpeningDto> Update(Guid id, JobOpeningInput input, CancellationToken ct) => Mediator.Send(new UpdateJobOpeningCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.RecruitmentManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteJobOpeningCommand(id), ct);
        return NoContent();
    }
}
