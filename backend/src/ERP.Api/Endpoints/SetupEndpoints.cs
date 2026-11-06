using ERP.Modularity.Features;
using MediatR;

namespace ERP.Api.Endpoints;

/// <summary>Business configuration: what kind of business this workspace is, which core areas it uses, its lists and its numbering.</summary>
public static class SetupEndpoints
{
    public static void MapSetup(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/setup").WithTags("Business setup");
        g.MapGet("/profile", (ISender s, CancellationToken ct) => s.Send(new GetBusinessProfile(), ct));
        g.MapPut("/capabilities", (CapabilitiesInput input, ISender s, CancellationToken ct) => s.Send(new SetCapabilities(input), ct));
        g.MapPut("/lists", (ProfileListsInput input, ISender s, CancellationToken ct) => s.Send(new SetProfileLists(input), ct));
        g.MapPost("/industry", (ChangeIndustryInput input, ISender s, CancellationToken ct) => s.Send(new ChangeIndustry(input), ct));
        g.MapGet("/sequences", (ISender s, CancellationToken ct) => s.Send(new ListNumberSequences(), ct));
        g.MapPut("/sequences/{documentType}", (string documentType, NumberSequenceInput input, ISender s, CancellationToken ct) => s.Send(new UpdateNumberSequence(documentType, input), ct));
    }
}
