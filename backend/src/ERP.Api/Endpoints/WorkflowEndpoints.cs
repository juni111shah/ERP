using ERP.Modularity;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Workflow;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record DecisionInput(string? Comment);

public static class WorkflowEndpoints
{
    public static void MapWorkflow(this RouteGroupBuilder api)
    {
        var approvals = api.MapGroup("/approvals").WithTags("Workflow");
        approvals.MapGet("", (string? scope, ISender sender, CancellationToken ct) => sender.Send(new ListApprovals(scope), ct)).RequireAuthorization();
        approvals.MapPost("", async (ApprovalInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new SubmitApproval(input), ct);
            return Results.Created($"/api/v1/approvals/{created.Id}", created);
        }).RequireAuthorization();
        approvals.MapPost("/{id:guid}/approve", (Guid id, DecisionInput? decision, ISender sender, CancellationToken ct) =>
            sender.Send(new DecideApproval(id, true, decision?.Comment), ct)).RequirePermission(CorePermissions.ApprovalsDecide);
        approvals.MapPost("/{id:guid}/reject", (Guid id, DecisionInput? decision, ISender sender, CancellationToken ct) =>
            sender.Send(new DecideApproval(id, false, decision?.Comment), ct)).RequirePermission(CorePermissions.ApprovalsDecide);
        approvals.MapPost("/{id:guid}/withdraw", (Guid id, ISender sender, CancellationToken ct) => sender.Send(new WithdrawApproval(id), ct)).RequireAuthorization();

        var policies = api.MapGroup("/approval-policies").WithTags("Workflow");
        policies.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListApprovalPolicies(), ct)).RequirePermission(CorePermissions.SettingsRead);
        policies.MapPost("", async (ApprovalPolicyInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new SaveApprovalPolicy(null, input), ct);
            return Results.Created($"/api/v1/approval-policies/{created.Id}", created);
        }).RequirePermission(CorePermissions.SettingsWrite);
        policies.MapPut("/{id:guid}", (Guid id, ApprovalPolicyInput input, ISender sender, CancellationToken ct) => sender.Send(new SaveApprovalPolicy(id, input), ct)).RequirePermission(CorePermissions.SettingsWrite);
        policies.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => { await sender.Send(new DeleteApprovalPolicy(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.SettingsWrite);

        var notifications = api.MapGroup("/notifications").WithTags("Workflow");
        notifications.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListNotifications(), ct)).RequireAuthorization();
        notifications.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkNotificationRead(id), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        var attachments = api.MapGroup("/attachments").WithTags("Workflow");
        attachments.MapGet("", (string ownerType, Guid ownerId, ISender sender, CancellationToken ct) => sender.Send(new ListAttachments(ownerType, ownerId), ct))
            .RequirePermission(CorePermissions.AttachmentsRead);
        attachments.MapPost("", async (HttpRequest request, ISender sender, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new FieldValidationException("file", "A file is required.");
            if (!Guid.TryParse(form["ownerId"], out var ownerId)) throw new FieldValidationException("ownerId", "Owner type and id are required.");
            await using var content = file.OpenReadStream();
            var created = await sender.Send(new UploadAttachment(form["ownerType"].ToString(), ownerId, file.FileName, file.ContentType, file.Length, content), ct);
            return Results.Created($"/api/v1/attachments/{created.Id}", created);
        }).RequirePermission(CorePermissions.AttachmentsWrite).DisableAntiforgery();
        attachments.MapGet("/{id:guid}/download", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var file = await sender.Send(new DownloadAttachment(id), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission(CorePermissions.AttachmentsRead);
        attachments.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteAttachment(id), ct);
            return Results.NoContent();
        }).RequirePermission(CorePermissions.AttachmentsWrite);
    }
}
