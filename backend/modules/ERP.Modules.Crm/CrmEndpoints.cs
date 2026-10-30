using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Crm;

internal static class CrmEndpoints
{
    private const string Base = "/api/v1/m/crm";

    public static void Map(RouteGroupBuilder group)
    {
        // Leads CRUD & Conversion
        group.MapPagedList<CrmLead, LeadSummaryDto>(
            "/leads",
            Permissions.LeadsRead,
            l => new LeadSummaryDto(
                l.Id,
                l.LegalEntityId,
                l.LeadNumber,
                l.FullName,
                l.CompanyName,
                l.JobTitle,
                l.Email,
                l.Phone,
                l.Source,
                l.Status,
                l.EstimatedValue,
                l.AssignedUserName,
                l.Notes,
                l.CreatedAt));

        group.MapGet("/leads/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var lead = await s.Send(new GetLeadByIdQuery(id), ct);
            return lead is null ? Results.NotFound() : Results.Ok(lead);
        }).RequirePermission(Permissions.LeadsRead);

        group.MapPost("/leads", async (CreateLeadCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/leads/{result.Id}", result);
        }).RequirePermission(Permissions.LeadsManage);

        group.MapPut("/leads/{id:guid}", async (Guid id, UpdateLeadCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.LeadsManage);

        group.MapPost("/leads/{id:guid}/convert", async (Guid id, ConvertLeadCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var oppId = await s.Send(cmd, ct);
            return Results.Ok(oppId);
        }).RequirePermission(Permissions.LeadsManage);

        // Opportunities CRUD & Pipeline
        group.MapPagedList<CrmOpportunity, OpportunitySummaryDto>(
            "/opportunities",
            Permissions.OpportunitiesRead,
            o => new OpportunitySummaryDto(
                o.Id,
                o.LegalEntityId,
                o.OpportunityNumber,
                o.Title,
                o.CustomerName,
                o.Stage,
                o.ProbabilityPercent,
                o.ExpectedRevenue,
                o.ExpectedCloseDate,
                o.AssignedUserName,
                o.CreatedAt));

        group.MapGet("/opportunities/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var opp = await s.Send(new GetOpportunityByIdQuery(id), ct);
            return opp is null ? Results.NotFound() : Results.Ok(opp);
        }).RequirePermission(Permissions.OpportunitiesRead);

        group.MapPost("/opportunities", async (CreateOpportunityCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/opportunities/{result.Id}", result);
        }).RequirePermission(Permissions.OpportunitiesManage);

        group.MapPost("/opportunities/{id:guid}/stage", async (Guid id, UpdateOpportunityStageCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.OpportunitiesManage);

        group.MapPost("/opportunities/{id:guid}/close", async (Guid id, CloseOpportunityCommand cmd, ISender s, CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest();
            var result = await s.Send(cmd, ct);
            return Results.Ok(result);
        }).RequirePermission(Permissions.OpportunitiesManage);

        group.MapGet("/opportunities/pipeline-summary", async (Guid? legalEntityId, ISender s, CancellationToken ct) =>
        {
            var stages = await s.Send(new GetPipelineSummaryQuery(legalEntityId), ct);
            return Results.Ok(stages);
        }).RequirePermission(Permissions.OpportunitiesRead);

        // Interactions / Activity Logging
        group.MapPagedList<CrmInteraction, CustomerInteractionDto>(
            "/interactions",
            Permissions.InteractionsRead,
            i => new CustomerInteractionDto(
                i.Id,
                i.LegalEntityId,
                i.RelatedEntityKind,
                i.RelatedEntityId,
                i.Type,
                i.Subject,
                i.Details,
                i.InteractionDate,
                i.PerformedByUserId,
                i.PerformedByName,
                i.FollowUpDate,
                i.IsResolved,
                i.CustomFields,
                i.CreatedAt));

        group.MapPost("/interactions", async (LogInteractionCommand cmd, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(cmd, ct);
            return Results.Created($"{Base}/interactions/{result.Id}", result);
        }).RequirePermission(Permissions.InteractionsManage);

        group.MapPost("/interactions/{id:guid}/resolve", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var success = await s.Send(new ResolveInteractionCommand(id), ct);
            return Results.Ok(success);
        }).RequirePermission(Permissions.InteractionsManage);

        group.MapGet("/interactions/by-entity", async (string kind, Guid id, ISender s, CancellationToken ct) =>
        {
            var list = await s.Send(new GetInteractionsByRelatedEntityQuery(kind, id), ct);
            return Results.Ok(list);
        }).RequirePermission(Permissions.InteractionsRead);

        // Analytics & KPIs
        group.MapGet("/analytics/kpis", async (Guid? legalEntityId, ISender s, CancellationToken ct) =>
        {
            var kpis = await s.Send(new GetCrmKpisQuery(legalEntityId), ct);
            return Results.Ok(kpis);
        }).RequirePermission(Permissions.AnalyticsRead);
    }
}
