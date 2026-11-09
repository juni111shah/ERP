using ERP.Application.Security;
using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Platform;

/// <summary>Business configuration: what kind of business this workspace is, which core areas it uses, its lists and its numbering.</summary>
[Route("api/v1/setup")]
public sealed class SetupController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("profile")]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<BusinessProfile> GetProfile(CancellationToken ct) => Mediator.Send(new GetBusinessProfileQuery(), ct);

    [HttpPut("capabilities")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<BusinessProfile> SetCapabilities(CapabilitiesInput input, CancellationToken ct) => Mediator.Send(new SetCapabilitiesCommand(input), ct);

    [HttpPut("lists")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<BusinessProfile> SetLists(ProfileListsInput input, CancellationToken ct) => Mediator.Send(new SetProfileListsCommand(input), ct);

    [HttpPost("industry")]
    [RequirePermission(CorePermissions.ModulesManage)]
    public Task<ChangeIndustryResult> ChangeIndustry(ChangeIndustryInput input, CancellationToken ct) => Mediator.Send(new ChangeIndustryCommand(input), ct);

    [HttpGet("sequences")]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<IReadOnlyList<NumberSequenceView>> ListSequences(CancellationToken ct) => Mediator.Send(new ListNumberSequencesQuery(), ct);

    [HttpPut("sequences/{documentType}")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<NumberSequenceView> UpdateSequence(string documentType, NumberSequenceInput input, CancellationToken ct)
        => Mediator.Send(new UpdateNumberSequenceCommand(documentType, input), ct);
}
