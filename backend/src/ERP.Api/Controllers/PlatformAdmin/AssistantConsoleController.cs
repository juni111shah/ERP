using ERP.Api.Infrastructure;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The AI assistants: their conversations, switches, limits and connected services (platform console).</summary>
[Route("api/v1/platform-admin/assistant")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class AssistantConsoleController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("conversations")]
    public Task<PagedResult<BrandConversationRow>> ListConversations([FromQuery] ListBrandConversationsQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<IReadOnlyList<BrandMessageRow>> GetConversation(Guid id, CancellationToken ct) => Mediator.Send(new GetBrandConversationQuery(id), ct);

    [HttpGet]
    public Task<AssistantConsoleView> Get(CancellationToken ct) => Mediator.Send(new GetAssistantConsoleQuery(), ct);

    [PlatformOwner]
    [HttpPut("visibility")]
    public Task<AssistantConsoleView> SetVisibility(AssistantVisibilityInput input, CancellationToken ct) => Mediator.Send(new SetAssistantVisibilityCommand(input), ct);

    [PlatformOwner]
    [HttpPut("limits")]
    public Task<AssistantConsoleView> SetLimits(AssistantLimitsInput input, CancellationToken ct) => Mediator.Send(new SetAssistantLimitsCommand(input), ct);

    [PlatformOwner]
    [HttpPost("try")]
    public IResult Try(AssistantTryInput input, CancellationToken ct) => new AssistantStreamResult(Mediator.CreateStream(new TryAssistantCommand(input), ct));

    [PlatformOwner]
    [HttpPost("providers")]
    public Task<AssistantConsoleView> CreateProvider(AiProviderInput input, CancellationToken ct) => Mediator.Send(new CreateAiProviderCommand(input), ct);

    [PlatformOwner]
    [HttpPut("providers/order")]
    public Task<AssistantConsoleView> ReorderProviders(AiProviderOrderInput input, CancellationToken ct) => Mediator.Send(new ReorderAiProvidersCommand(input), ct);

    [PlatformOwner]
    [HttpPut("providers/{id:guid}")]
    public Task<AssistantConsoleView> UpdateProvider(Guid id, AiProviderInput input, CancellationToken ct) => Mediator.Send(new UpdateAiProviderCommand(id, input), ct);

    [PlatformOwner]
    [HttpDelete("providers/{id:guid}")]
    public Task<AssistantConsoleView> DeleteProvider(Guid id, CancellationToken ct) => Mediator.Send(new DeleteAiProviderCommand(id), ct);

    [PlatformOwner]
    [HttpPost("providers/{id:guid}/test")]
    public Task<AssistantConsoleView> TestProvider(Guid id, CancellationToken ct) => Mediator.Send(new TestAiProviderCommand(id), ct);
}
