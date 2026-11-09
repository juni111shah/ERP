using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Recruitment: candidates through the pipeline, up to hiring.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/candidates")]
public sealed class CandidatesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.RecruitmentRead)]
    public Task<PagedResult<CandidateRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, CandidateRow.Projection, P.RecruitmentRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.RecruitmentRead)]
    public Task<CandidateDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetCandidateQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.RecruitmentManage)]
    public async Task<IActionResult> Create(CandidateInput input, CancellationToken ct)
    {
        var c = await Mediator.Send(new CreateCandidateCommand(input), ct);
        return Created($"{Base}/candidates/{c.Id}", c);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.RecruitmentManage)]
    public Task<CandidateDto> Update(Guid id, CandidateInput input, CancellationToken ct) => Mediator.Send(new UpdateCandidateCommand(id, input), ct);

    [HttpPost("{id:guid}/move")]
    [RequirePermission(P.RecruitmentManage)]
    public Task<CandidateDto> Move(Guid id, MoveCandidateInput input, CancellationToken ct) => Mediator.Send(new MoveCandidateCommand(id, input), ct);

    [HttpPost("{id:guid}/hire")]
    [RequirePermission(P.RecruitmentManage)]
    public async Task<IActionResult> Hire(Guid id, HireInput input, CancellationToken ct)
        => Created($"{Base}/employees", await Mediator.Send(new HireCandidateCommand(id, input), ct));
}
