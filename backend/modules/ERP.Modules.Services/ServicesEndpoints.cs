using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Services;

public sealed record UpdateTimeEntryBody(DateOnly ActivityDate, decimal Hours, decimal HourlyRate, string TaskDescription, bool IsBillable, Guid? ConsultantUserId = null, string? ConsultantName = null, string? CustomFields = null);
public sealed record ApproveTimeEntryBody(Guid? Id = null, Guid? ApprovedByUserId = null);

internal static class ServicesEndpoints
{
    private const string Base = "/api/v1/m/services";

    public static void Map(RouteGroupBuilder group)
    {
        // Engagements
        group.MapPagedList<ServiceEngagement, EngagementSummaryDto>(
            "/engagements",
            Permissions.EngagementsRead,
            e => new EngagementSummaryDto(
                e.Id,
                e.LegalEntityId,
                e.EngagementCode,
                e.Title,
                e.ClientId,
                e.ClientName,
                e.Type,
                e.Status,
                e.ContractValue,
                e.BudgetedHours,
                e.ConsumedHours,
                e.StartDate,
                e.TargetEndDate,
                e.LeadConsultantName,
                e.CreatedAt));

        group.MapGet("/engagements/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var engagement = await s.Send(new GetEngagementByIdQuery(id), ct);
            return engagement is null ? Results.NotFound() : Results.Ok(engagement);
        }).RequirePermission(Permissions.EngagementsRead);

        group.MapPost("/engagements", async (CreateEngagementCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/engagements/{result.Id}", result);
        }).RequirePermission(Permissions.EngagementsManage);

        group.MapPost("/engagements/{id:guid}/invoice-time", (Guid id, EngagementTimeInvoiceBody? b, ISender s, CancellationToken ct) =>
            s.Send(new InvoiceEngagementTime(id, b?.TimeEntryIds, b?.From, b?.To, b?.InvoiceDate, b?.DueDate, b?.TaxCodeId, b?.Notes), ct)).RequirePermission(Permissions.EngagementsManage);

        group.MapPost("/engagements/{id:guid}/status", async (Guid id, UpdateEngagementStatusCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.EngagementsManage);

        // Retainers
        group.MapPagedList<RetainerContract, RetainerSummaryDto>(
            "/retainers",
            Permissions.RetainersRead,
            r => new RetainerSummaryDto(
                r.Id,
                r.LegalEntityId,
                r.RetainerNumber,
                r.EngagementId,
                r.ClientName,
                r.Title,
                r.Cycle,
                r.Status,
                r.RecurringFee,
                r.IncludedHours,
                r.StartDate,
                r.CreatedAt));

        group.MapGet("/retainers/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var retainer = await s.Send(new GetRetainerByIdQuery(id), ct);
            return retainer is null ? Results.NotFound() : Results.Ok(retainer);
        }).RequirePermission(Permissions.RetainersRead);

        group.MapPost("/retainers", async (CreateRetainerCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/retainers/{result.Id}", result);
        }).RequirePermission(Permissions.RetainersManage);

        group.MapPost("/retainers/{id:guid}/invoice", (Guid id, RetainerInvoiceBody? b, ISender s, CancellationToken ct) =>
            s.Send(new GenerateRetainerInvoice(id, b?.PeriodDate, b?.InvoiceDate, b?.DueDate, b?.TaxCodeId, b?.Notes), ct)).RequirePermission(Permissions.RetainersManage);

        group.MapPost("/retainers/{id:guid}/status", async (Guid id, UpdateRetainerStatusCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.RetainersManage);

        // Billing Milestones
        group.MapPagedList<BillingMilestone, MilestoneSummaryDto>(
            "/milestones",
            Permissions.MilestonesRead,
            m => new MilestoneSummaryDto(
                m.Id,
                m.LegalEntityId,
                m.MilestoneCode,
                m.EngagementId,
                m.Title,
                m.Amount,
                m.DueDate,
                m.CompletionDate,
                m.Status,
                m.CreatedAt));

        group.MapGet("/milestones/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new GetMilestoneQuery(id), ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.MilestonesRead);

        group.MapPost("/milestones", async (CreateMilestoneCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/milestones/{result.Id}", result);
        }).RequirePermission(Permissions.MilestonesManage);

        group.MapPost("/milestones/{id:guid}/status", async (Guid id, UpdateMilestoneStatusCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.MilestonesManage);

        group.MapPost("/milestones/{id:guid}/invoice", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var invoice = await s.Send(new InvoiceMilestoneCommand(id), ct);
            return Results.Ok(invoice);
        }).RequirePermission(Permissions.MilestonesManage);

        // Time Entries
        group.MapPagedList<TimeEntryRecord, TimeEntrySummaryDto>(
            "/time-entries",
            Permissions.TimeEntriesRead,
            t => new TimeEntrySummaryDto(
                t.Id,
                t.LegalEntityId,
                t.EngagementId,
                t.ConsultantName,
                t.ActivityDate,
                t.Hours,
                t.HourlyRate,
                t.TotalAmount,
                t.TaskDescription,
                t.IsBillable,
                t.IsApproved,
                t.CreatedAt,
                t.InvoiceId));

        group.MapPost("/time-entries", async (LogTimeEntryCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/time-entries/{result.Id}", result);
        }).RequirePermission(Permissions.TimeEntriesManage);

        group.MapPut("/time-entries/{id:guid}", (Guid id, UpdateTimeEntryBody b, ISender s, CancellationToken ct) =>
            s.Send(new UpdateTimeEntryCommand(id, b.ActivityDate, b.Hours, b.HourlyRate, b.TaskDescription, b.IsBillable, b.ConsultantUserId, b.ConsultantName, b.CustomFields), ct)).RequirePermission(Permissions.TimeEntriesManage);

        group.MapDelete("/time-entries/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteTimeEntryCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.TimeEntriesManage);

        // The approver is the signed-in user. A body is optional; what older clients send in it (id, approvedByUserId) changes nothing except that a mismatched id is refused.
        group.MapPost("/time-entries/{id:guid}/approve", async (Guid id, ApproveTimeEntryBody? b, ISender s, CancellationToken ct) =>
        {
            if (b?.Id is { } bodyId && bodyId != id) return Results.BadRequest();
            var result = await s.Send(new ApproveTimeEntryCommand(id), ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.TimeEntriesManage);

        // Analytics
        group.MapGet("/analytics/kpis", async (Guid legalEntityId, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new GetServicesKpisQuery(legalEntityId), ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.AnalyticsRead);
    }
}
