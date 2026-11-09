using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Assistant;

/// <summary>The AI assistant inside a workspace. It is off until the workspace's administrator enables this module and the platform owner has cleared a service for client data.</summary>
public sealed class AssistantModule : ModuleBase
{
    public const string Key = "assistant";
    public const string ChatsUse = "assistant.chats.use";
    public const string RetentionDaysSetting = "assistant.retentionDays";
    public const string DailyMessagesSetting = "assistant.dailyMessagesPerUser";

    public override ModuleManifest Manifest { get; } = new(Key, "AI assistant",
        "Ask questions about your business and get answers from your own records. Enabling it sends the information needed for each answer to an AI service provider.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions => [PermissionDefinition.Of(ChatsUse, "Use the AI assistant")];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new(RetentionDaysSetting, "Days to keep assistant chats", "90", "number"),
        new(DailyMessagesSetting, "Messages each person can send per day", "30", "number"),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IScheduledJob, AssistantChatCleanupJob>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<AssistantConversation>(b =>
        {
            b.ToTable("ast_Conversations");
            b.Property(x => x.Title).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.UserId, x.LastMessageAt });
        });
        mb.Entity<AssistantChatMessage>(b =>
        {
            b.ToTable("ast_Messages");
            b.Property(x => x.Text).HasMaxLength(8000).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.ConversationId, x.At });
            b.HasIndex(x => new { x.OrgId, x.UserId, x.At });
        });
        mb.Entity<AssistantPendingAction>(b =>
        {
            b.ToTable("ast_PendingActions");
            b.Property(x => x.ActionKey).HasMaxLength(60).IsRequired();
            b.Property(x => x.ArgumentsJson).HasMaxLength(8000).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.LinesJson).HasMaxLength(8000).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Message).HasMaxLength(500);
            b.Property(x => x.Link).HasMaxLength(300);
            b.HasIndex(x => new { x.OrgId, x.ConversationId });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/status", (ISender s, CancellationToken ct) => s.Send(new GetWorkspaceAssistantStatus(), ct)).RequirePermission(ChatsUse);
        group.MapGet("/conversations", (ISender s, CancellationToken ct) => s.Send(new ListAssistantConversations(), ct)).RequirePermission(ChatsUse);
        group.MapGet("/conversations/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAssistantConversation(id), ct)).RequirePermission(ChatsUse);
        group.MapDelete("/conversations/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteAssistantConversation(id), ct); return Results.NoContent(); }).RequirePermission(ChatsUse);
        // Asking changes nothing in the business, so a view-only workspace can still ask. Confirming a change stays blocked there.
        group.MapPost("/messages", (WorkspaceMessageInput input, ISender s, CancellationToken ct) => new AssistantStreamResult(s.CreateStream(new AskWorkspaceAssistant(input), ct)))
            .RequirePermission(ChatsUse).WithMetadata(new AllowedInReadOnlyWorkspaceAttribute());
        group.MapPost("/actions/{id:guid}/confirm", (Guid id, ISender s, CancellationToken ct) => s.Send(new ConfirmAssistantAction(id), ct)).RequirePermission(ChatsUse);
        group.MapPost("/actions/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelAssistantAction(id), ct)).RequirePermission(ChatsUse).WithMetadata(new AllowedInReadOnlyWorkspaceAttribute());
    }
}
