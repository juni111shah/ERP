using ERP.Api.Infrastructure;
using ERP.Application.Assistant;
using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Features;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Api.Endpoints;

public static class PlatformAdminEndpoints
{
    /// <summary>The public side: anyone may read the price list and register.</summary>
    public static void MapRegistration(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/public").WithTags("Registration");
        g.MapGet("/signup-config", (ISender sender, CancellationToken ct) => sender.Send(new GetSignupConfig(), ct)).AllowAnonymous();
        g.MapGet("/legal", (ISender sender, CancellationToken ct) => sender.Send(new GetLegalDocuments(), ct)).AllowAnonymous();
        g.MapGet("/modules", (ISender sender, CancellationToken ct) => sender.Send(new ListPlatformModules(), ct)).AllowAnonymous();
        g.MapGet("/sign-in-key-available", (string key, ISender sender, CancellationToken ct) => sender.Send(new CheckSignInKey(key), ct)).AllowAnonymous().RequireRateLimiting("signup-check");
        g.MapPost("/signups", (SignupInput input, HttpContext http, ISender sender, CancellationToken ct) =>
            sender.Send(new RegisterWorkspace(input, http.Connection.RemoteIpAddress?.ToString()), ct)).AllowAnonymous().RequireRateLimiting("login");
        g.MapPost("/signups/verify", (VerifySignupInput input, ISender sender, CancellationToken ct) => sender.Send(new VerifySignup(input.Token), ct)).AllowAnonymous().RequireRateLimiting("login");
        g.MapGet("/content", (ISender sender, CancellationToken ct) => sender.Send(new GetPublicContent(), ct)).AllowAnonymous();
        g.MapGet("/assistant/status", (ISender sender, CancellationToken ct) => sender.Send(new GetBrandAssistantStatus(), ct)).AllowAnonymous();
        g.MapPost("/assistant/messages", (BrandMessageInput input, ISender sender, CancellationToken ct) => new AssistantStreamResult(sender.CreateStream(new AskBrandAssistant(input), ct))).AllowAnonymous().RequireRateLimiting("assistant-public");
        g.MapPost("/assistant/messages/{id:guid}/feedback", async (Guid id, BrandFeedbackInput input, ISender sender, CancellationToken ct) => { await sender.Send(new SetBrandMessageFeedback(id, input), ct); return Results.NoContent(); }).AllowAnonymous().RequireRateLimiting("assistant-public");
        g.MapPost("/assistant/leads", async (BrandLeadInput input, ISender sender, CancellationToken ct) => { await sender.Send(new SubmitBrandLead(input), ct); return Results.NoContent(); }).AllowAnonymous().RequireRateLimiting("login");
    }

    /// <summary>A client's own plan and invoices.</summary>
    public static void MapBilling(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/billing").WithTags("Billing");
        g.MapGet("/subscription", (ISender sender, CancellationToken ct) => sender.Send(new GetBilling(), ct)).RequirePermission(CorePermissions.BillingRead);
        g.MapGet("/invoices/{id:guid}", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new GetMyInvoice(id), ct)).RequirePermission(CorePermissions.BillingRead);
        g.MapPost("/change-request", (PlanChangeInput input, ISender sender, CancellationToken ct) => sender.Send(new RequestPlanChange(input), ct)).RequirePermission(CorePermissions.BillingManage);
        g.MapDelete("/change-request", (ISender sender, CancellationToken ct) => sender.Send(new WithdrawPlanChange(), ct)).RequirePermission(CorePermissions.BillingManage);
    }

    /// <summary>The founder's console. The policy is on the group, so no route here can be left open by accident.</summary>
    public static void MapPlatformAdmin(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/platform-admin").WithTags("Platform console").RequireAuthorization(PlatformAdminRequirement.Policy);

        g.MapGet("/dashboard", (ISender s, CancellationToken ct) => s.Send(new GetPlatformDashboard(), ct));
        g.MapGet("/modules", (ISender s, CancellationToken ct) => s.Send(new ListPlatformModules(), ct));

        g.MapGet("/plans", (ISender s, CancellationToken ct) => s.Send(new ListPlans(), ct));
        g.MapPost("/plans", (PlanInput input, ISender s, CancellationToken ct) => s.Send(new CreatePlan(input), ct));
        g.MapPut("/plans/{id:guid}", (Guid id, PlanInput input, ISender s, CancellationToken ct) => s.Send(new UpdatePlan(id, input), ct));
        g.MapPost("/plans/{id:guid}/{action:regex(^(archive|restore)$)}", (Guid id, string action, ISender s, CancellationToken ct) => s.Send(new SetPlanArchived(id, action == "archive"), ct));

        g.MapGet("/clients", ([AsParameters] ListClients q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapPost("/clients", (CreateClientInput input, ISender s, CancellationToken ct) => s.Send(new CreateClient(input), ct));
        g.MapGet("/clients/{orgId:guid}", (Guid orgId, ISender s, CancellationToken ct) => s.Send(new GetClient(orgId), ct));
        g.MapPut("/clients/{orgId:guid}/plan", (Guid orgId, SetClientPlanInput input, ISender s, CancellationToken ct) => s.Send(new SetClientPlan(orgId, input), ct));
        g.MapPut("/clients/{orgId:guid}/details", (Guid orgId, ClientNotesInput input, ISender s, CancellationToken ct) => s.Send(new UpdateClientNotes(orgId, input), ct));
        g.MapPost("/clients/{orgId:guid}/extend-trial", (Guid orgId, ExtendTrialInput input, ISender s, CancellationToken ct) => s.Send(new ExtendClientTrial(orgId, input.Days), ct));
        g.MapPost("/clients/{orgId:guid}/activate", (Guid orgId, ActivateInput input, ISender s, CancellationToken ct) => s.Send(new ActivateClient(orgId, input.IssueInvoice), ct));
        g.MapPost("/clients/{orgId:guid}/suspend", (Guid orgId, ReasonInput input, ISender s, CancellationToken ct) => s.Send(new SuspendClient(orgId, input.Reason), ct));
        g.MapPost("/clients/{orgId:guid}/reactivate", (Guid orgId, ISender s, CancellationToken ct) => s.Send(new ReactivateClient(orgId), ct));
        g.MapPost("/clients/{orgId:guid}/cancel", (Guid orgId, ReasonInput input, ISender s, CancellationToken ct) => s.Send(new CancelClient(orgId, input.Reason), ct));
        g.MapPost("/clients/{orgId:guid}/decline-plan-request", (Guid orgId, ISender s, CancellationToken ct) => s.Send(new DeclinePlanRequest(orgId), ct));
        g.MapPost("/clients/{orgId:guid}/invoices", (Guid orgId, IssueInvoiceInput input, ISender s, CancellationToken ct) => s.Send(new IssueClientInvoice(orgId, input), ct));
        g.MapPost("/clients/{orgId:guid}/users/{userId:guid}/temporary-password", (Guid orgId, Guid userId, ISender s, CancellationToken ct) => s.Send(new ResetClientUserPassword(orgId, userId), ct));
        g.MapPost("/clients/{orgId:guid}/modules/{key}/{action:regex(^(enable|disable)$)}", (Guid orgId, string key, string action, ISender s, CancellationToken ct) => s.Send(new SetClientModule(orgId, key, action == "enable"), ct));

        g.MapGet("/invoices", ([AsParameters] ListPlatformInvoices q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapGet("/invoices/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPlatformInvoice(id), ct));
        g.MapPost("/invoices/{id:guid}/payment", (Guid id, RecordPaymentInput input, ISender s, CancellationToken ct) => s.Send(new RecordInvoicePayment(id, input), ct));
        g.MapPost("/invoices/{id:guid}/void", (Guid id, ReasonInput input, ISender s, CancellationToken ct) => s.Send(new VoidPlatformInvoice(id, input.Reason), ct));
        g.MapPost("/invoices/{id:guid}/reopen", (Guid id, ISender s, CancellationToken ct) => s.Send(new ReopenPlatformInvoice(id), ct));

        g.MapGet("/signups", ([AsParameters] ListSignups q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapPost("/signups/{id:guid}/approve", (Guid id, ISender s, CancellationToken ct) => s.Send(new ApproveSignup(id), ct));
        g.MapPost("/signups/{id:guid}/reject", (Guid id, ReasonInput input, ISender s, CancellationToken ct) => s.Send(new RejectSignup(id, input.Reason), ct));

        g.MapGet("/settings", (ISender s, CancellationToken ct) => s.Send(new GetPlatformSettings(), ct));
        g.MapPut("/settings", (PlatformSettingsInput input, ISender s, CancellationToken ct) => s.Send(new UpdatePlatformSettings(input), ct));
        g.MapGet("/activity", ([AsParameters] ListPlatformActivity q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapPost("/billing-run", (ISender s, CancellationToken ct) => s.Send(new RunBillingNow(), ct));

        g.MapGet("/leads", ([AsParameters] ListBrandLeads q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapPut("/leads/{id:guid}/status/{status:regex(^(new|contacted|won|lost)$)}", (Guid id, string status, ISender s, CancellationToken ct) => s.Send(new SetBrandLeadStatus(id, Enum.Parse<BrandLeadStatus>(status, ignoreCase: true)), ct));
        g.MapDelete("/leads/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteBrandLead(id), ct); return Results.NoContent(); });
        g.MapGet("/assistant/conversations", ([AsParameters] ListBrandConversations q, ISender s, CancellationToken ct) => s.Send(q, ct));
        g.MapGet("/assistant/conversations/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetBrandConversation(id), ct));
        g.MapPost("/assistant/conversations/{id:guid}/reviewed", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new MarkBrandConversationReviewed(id), ct); return Results.NoContent(); });
        g.MapDelete("/assistant/conversations/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteBrandConversation(id), ct); return Results.NoContent(); });
        g.MapGet("/assistant", (ISender s, CancellationToken ct) => s.Send(new GetAssistantConsole(), ct));
        g.MapPut("/assistant/visibility", (AssistantVisibilityInput input, ISender s, CancellationToken ct) => s.Send(new SetAssistantVisibility(input), ct));
        g.MapPut("/assistant/limits", (AssistantLimitsInput input, ISender s, CancellationToken ct) => s.Send(new SetAssistantLimits(input), ct));
        g.MapPost("/assistant/try", (AssistantTryInput input, ISender s, CancellationToken ct) => new AssistantStreamResult(s.CreateStream(new TryAssistant(input), ct)));
        g.MapPost("/assistant/providers", (AiProviderInput input, ISender s, CancellationToken ct) => s.Send(new CreateAiProvider(input), ct));
        g.MapPut("/assistant/providers/order", (AiProviderOrderInput input, ISender s, CancellationToken ct) => s.Send(new ReorderAiProviders(input), ct));
        g.MapPut("/assistant/providers/{id:guid}", (Guid id, AiProviderInput input, ISender s, CancellationToken ct) => s.Send(new UpdateAiProvider(id, input), ct));
        g.MapDelete("/assistant/providers/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new DeleteAiProvider(id), ct));
        g.MapPost("/assistant/providers/{id:guid}/test", (Guid id, ISender s, CancellationToken ct) => s.Send(new TestAiProvider(id), ct));

        g.MapGet("/team", (ISender s, CancellationToken ct) => s.Send(new ListPlatformTeam(), ct));
        g.MapPost("/team", (TeamMemberInput input, ISender s, CancellationToken ct) => s.Send(new AddPlatformTeamMember(input), ct));
        g.MapDelete("/team/{userId:guid}", async (Guid userId, ISender s, CancellationToken ct) =>
        {
            await s.Send(new RemovePlatformTeamMember(userId), ct);
            return Results.NoContent();
        });
    }
}

public sealed record VerifySignupInput(string Token);
