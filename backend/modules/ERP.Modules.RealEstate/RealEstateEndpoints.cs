using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate;

public sealed record UnitStatusBody(UnitAction Action, DateOnly? Until = null, string? Reason = null);
public sealed record TerminateBody(DateOnly Date, string Reason);
public sealed record HoldingOverBody(bool HoldingOver);
public sealed record WorkOrderStatusBody(WorkOrderStatus Status, decimal? ActualCost = null, string? ResolutionNotes = null);
public sealed record PayInstalmentBody(Guid BankAccountId, DateOnly Date);
public sealed record PayAgentBody(Guid BankAccountId, DateOnly Date);
public sealed record DepositBody(Guid BankAccountId, DateOnly Date, decimal Amount);
public sealed record TransferDepositBody(Guid ToLeaseId);
public sealed record BillingRunBody(DateOnly AsOf, Guid? LegalEntityId = null);
public sealed record CompleteInspectionBody(bool RaiseWorkOrders = false);
public sealed record RenewalDraftsBody(List<Guid> LeaseIds, decimal UpliftPercent);
public sealed record DealStageBody(DealStage Stage, string? LostReason = null);

/// <summary>Thin controllers: every endpoint sends one MediatR request (see docs/ARCHITECTURE.md, request flow).</summary>
internal static class RealEstateEndpoints
{
    private const string Base = "/api/v1/m/realestate";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/status", () => Results.Ok(new { module = RealEstateModule.Key, message = "Real estate module is installed and enabled." })).RequirePermission(P.PropertiesRead);
        MapUnitTypes(group.MapGroup("/unit-types"));
        MapProperties(group.MapGroup("/properties"));
        MapUnits(group.MapGroup("/units"));
        MapLeases(group.MapGroup("/leases"));
        MapWorkOrders(group.MapGroup("/work-orders"));
        MapPm(group.MapGroup("/pm-schedules"));
        MapInspections(group.MapGroup("/inspections"));
        MapRenewals(group.MapGroup("/renewals"));
        MapPortal(group.MapGroup("/portal"));
        MapDeals(group.MapGroup("/deals"));
        MapAcquisitions(group.MapGroup("/acquisitions"));
        MapBilling(group.MapGroup("/billing"));
        MapReports(group.MapGroup("/reports"));
    }

    private static void MapReports(RouteGroupBuilder g)
    {
        const string perm = CorePermissions.ReportsRead;
        g.MapGet("/owner-statement", (Guid ownerPartyId, DateOnly from, DateOnly to, ISender s, CancellationToken ct) => s.Send(new GetOwnerStatement(ownerPartyId, from, to), ct)).RequirePermission(perm);
        g.MapGet("/rent-roll", (DateOnly asOf, Guid[]? propertyIds, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetRentRoll(asOf, propertyIds, legalEntityId), ct)).RequirePermission(perm);
        g.MapGet("/occupancy", (Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetOccupancyReport(legalEntityId), ct)).RequirePermission(perm);
        g.MapGet("/portfolio-performance", (DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetPortfolioPerformance(from, to, legalEntityId), ct)).RequirePermission(perm);
        g.MapGet("/lease-expiries", (int? months, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetLeaseExpiries(months ?? 12, legalEntityId), ct)).RequirePermission(perm);
        g.MapGet("/maintenance-costs", (DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetMaintenanceCosts(from, to, legalEntityId), ct)).RequirePermission(perm);
        g.MapGet("/commissions", (DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCommissionsReport(from, to, legalEntityId), ct)).RequirePermission(perm);
    }

    private static void MapAcquisitions(RouteGroupBuilder g)
    {
        g.MapPagedList<Acquisition, AcquisitionSummaryDto>("", P.AcquisitionsRead, a => new AcquisitionSummaryDto(a.Id, a.Number, a.PropertyId, a.SellerName, a.PurchaseDate, a.PurchasePrice, a.DownPayment, a.Status, a.CurrencyCode));
        g.MapGet("/overview", (ISender s, CancellationToken ct) => s.Send(new GetAcquisitionOverview(), ct)).RequirePermission(P.AcquisitionsRead);
        g.MapGet("/funding", (ISender s, CancellationToken ct) => s.Send(new ListFundingSources(), ct)).RequirePermission(P.AcquisitionsRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAcquisition(id), ct)).RequirePermission(P.AcquisitionsRead);
        g.MapPost("", async (AcquisitionInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateAcquisition(input), ct);
            return Results.Created($"{Base}/acquisitions/{created.Id}", created);
        }).RequirePermission(P.AcquisitionsManage);
        g.MapPut("/{id:guid}", (Guid id, AcquisitionInput input, ISender s, CancellationToken ct) => s.Send(new UpdateAcquisition(id, input), ct)).RequirePermission(P.AcquisitionsManage);
        g.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelAcquisition(id), ct)).RequirePermission(P.AcquisitionsManage);
        g.MapPost("/{id:guid}/complete", (Guid id, ISender s, CancellationToken ct) => s.Send(new CompleteAcquisition(id), ct)).RequirePermission(P.AcquisitionsComplete);
        g.MapPost("/instalments/{id:guid}/pay", (Guid id, PayInstalmentBody b, ISender s, CancellationToken ct) => s.Send(new PayFundingInstalment(id, b.BankAccountId, b.Date), ct)).RequirePermission(P.FundingPay);
    }

    private static void MapBilling(RouteGroupBuilder g)
    {
        g.MapPost("/run", (BillingRunBody body, ISender s, CancellationToken ct) => s.Send(new RunBilling(body.AsOf, body.LegalEntityId), ct)).RequirePermission(P.BillingRun);
        g.MapPost("/instalments/{id:guid}/invoice", (Guid id, ISender s, CancellationToken ct) => s.Send(new InvoiceInstalment(id), ct)).RequirePermission(P.BillingRun);
    }

    private static void MapUnitTypes(RouteGroupBuilder g)
    {
        g.MapPagedList<UnitType, UnitTypeDto>("", P.UnitTypesRead, t => new UnitTypeDto(t.Id, t.Code, t.Name, t.DefaultUsage, t.DefaultBedrooms, t.IsActive));
        g.MapPost("", async (UnitTypeInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateUnitType(input), ct);
            return Results.Created($"{Base}/unit-types/{created.Id}", created);
        }).RequirePermission(P.UnitTypesManage);
        g.MapPut("/{id:guid}", (Guid id, UnitTypeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateUnitType(id, input), ct)).RequirePermission(P.UnitTypesManage);
    }

    private static void MapProperties(RouteGroupBuilder g)
    {
        g.MapPagedList<Property, PropertyDto>("", P.PropertiesRead, Docs.Property, PropertyDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetProperty(id), ct)).RequirePermission(P.PropertiesRead);
        g.MapPost("", async (PropertyInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateProperty(input), ct);
            return Results.Created($"{Base}/properties/{created.Id}", created);
        }).RequirePermission(P.PropertiesManage);
        g.MapPut("/{id:guid}", (Guid id, PropertyInput input, ISender s, CancellationToken ct) => s.Send(new UpdateProperty(id, input), ct)).RequirePermission(P.PropertiesManage);
    }

    private static void MapUnits(RouteGroupBuilder g)
    {
        g.MapGet("", ([AsParameters] ListUnits query, ISender s, CancellationToken ct) => s.Send(query, ct)).RequirePermission(P.UnitsRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetUnit(id), ct)).RequirePermission(P.UnitsRead);
        g.MapGet("/{id:guid}/rent-history", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetUnitRentHistory(id), ct)).RequirePermission(P.UnitsRead);
        g.MapPost("", async (UnitInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateUnit(input), ct);
            return Results.Created($"{Base}/units/{created.Id}", created);
        }).RequirePermission(P.UnitsManage);
        g.MapPost("/bulk", async (BulkUnitsInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/units", await s.Send(new BulkCreateUnits(input), ct)))
            .RequirePermission(P.UnitsManage);
        g.MapPut("/{id:guid}", (Guid id, UnitInput input, ISender s, CancellationToken ct) => s.Send(new UpdateUnit(id, input), ct)).RequirePermission(P.UnitsManage);
        // Reserve/release need units.reserve, the rest units.manage: the command declares the permission for its action.
        g.MapPost("/{id:guid}/status", (Guid id, UnitStatusBody body, ISender s, CancellationToken ct) => s.Send(new ChangeUnitStatus(id, body.Action, body.Until, body.Reason), ct))
            .RequireAuthorization();
    }

    private static void MapLeases(RouteGroupBuilder g)
    {
        g.MapGet("", ([AsParameters] ListLeases query, ISender s, CancellationToken ct) => s.Send(query, ct)).RequirePermission(P.LeasesRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetLease(id), ct)).RequirePermission(P.LeasesRead);
        g.MapGet("/{id:guid}/deposit", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetSecurityDeposit(id), ct)).RequirePermission(P.LeasesRead);
        g.MapPost("/{id:guid}/deposit", (Guid id, DepositBody b, ISender s, CancellationToken ct) => s.Send(new RecordSecurityDeposit(id, b.BankAccountId, b.Date, b.Amount), ct)).RequirePermission(P.DepositsManage);
        g.MapPost("/{id:guid}/deposit/settle", (Guid id, SettleDepositInput input, ISender s, CancellationToken ct) => s.Send(new SettleSecurityDeposit(id, input), ct)).RequirePermission(P.DepositsManage);
        g.MapPost("/{id:guid}/deposit/transfer", (Guid id, TransferDepositBody b, ISender s, CancellationToken ct) => s.Send(new TransferSecurityDeposit(id, b.ToLeaseId), ct)).RequirePermission(P.DepositsManage);
        g.MapPost("/{id:guid}/termination-credits", (Guid id, ISender s, CancellationToken ct) => s.Send(new CreateTerminationCredits(id), ct)).RequirePermission(P.LeasesTerminate);
        g.MapGet("/{id:guid}/schedule", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetLease(id), ct)).Schedule).RequirePermission(P.LeasesRead);
        g.MapPost("", async (LeaseInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateLease(input), ct);
            return Results.Created($"{Base}/leases/{created.Id}", created);
        }).RequireAuthorization(); // leases.create, or leases.activate with ActivateNow: the command declares which
        g.MapPut("/{id:guid}", (Guid id, LeaseInput input, ISender s, CancellationToken ct) => s.Send(new UpdateLease(id, input), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/activate", (Guid id, ISender s, CancellationToken ct) => s.Send(new ActivateLease(id), ct)).RequirePermission(P.LeasesActivate);
        g.MapPost("/{id:guid}/renew", async (Guid id, RenewalInput? input, ISender s, CancellationToken ct) =>
        {
            var renewal = await s.Send(new RenewLease(id, input ?? new(null, null, null, null, null)), ct);
            return Results.Created($"{Base}/leases/{renewal.Id}", renewal);
        }).RequirePermission(P.LeasesRenew);
        g.MapPost("/{id:guid}/terminate", (Guid id, TerminateBody body, ISender s, CancellationToken ct) => s.Send(new TerminateLease(id, body.Date, body.Reason), ct)).RequirePermission(P.LeasesTerminate);
        g.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelLease(id), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/holding-over", (Guid id, HoldingOverBody body, ISender s, CancellationToken ct) => s.Send(new FlagHoldingOver(id, body.HoldingOver), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/move-out", (Guid id, ISender s, CancellationToken ct) => s.Send(new ConfirmLeaseMoveOut(id), ct)).RequirePermission(P.LeasesTerminate);
        g.MapPost("/process-expiries", (ISender s, CancellationToken ct) => s.Send(new ProcessLeaseExpiries(), ct)).RequirePermission(P.LeasesActivate);
    }

    /// <summary>Tenant portal: the signed-in customer's own leases, schedule and maintenance requests.</summary>
    private static void MapPortal(RouteGroupBuilder g)
    {
        g.MapGet("/leases", (ISender s, CancellationToken ct) => s.Send(new GetPortalLeases(), ct)).RequireAuthorization("portal");
        g.MapGet("/requests", (ISender s, CancellationToken ct) => s.Send(new ListPortalRequests(), ct)).RequireAuthorization("portal");
        g.MapPost("/requests", async (PortalRequestInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/portal/requests", await s.Send(new CreatePortalRequest(input), ct))).RequireAuthorization("portal");
    }

    private static void MapPm(RouteGroupBuilder g)
    {
        g.MapPagedList<PmSchedule, PmScheduleDto>("", P.PmRead, s => new PmScheduleDto(s.Id, s.PropertyId, s.UnitId, s.Title, s.Category, s.Priority, s.FrequencyMonths, s.NextDue, s.LeadDays, s.EstimatedCost, s.IsActive, s.LastGeneratedOn, s.Notes));
        g.MapPost("", async (PmScheduleInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/pm-schedules", await s.Send(new CreatePmSchedule(input), ct))).RequirePermission(P.PmManage);
        g.MapPut("/{id:guid}", (Guid id, PmScheduleInput input, ISender s, CancellationToken ct) => s.Send(new UpdatePmSchedule(id, input), ct)).RequirePermission(P.PmManage);
        g.MapPost("/run", (ISender s, CancellationToken ct) => s.Send(new RunPreventiveMaintenance(), ct)).RequirePermission(P.PmManage);
    }

    private static void MapInspections(RouteGroupBuilder g)
    {
        g.MapPagedList<Inspection, InspectionRow>("", P.InspectionsRead, i => new InspectionRow(i.Id, i.PropertyId, i.UnitId, i.LeaseId, i.Kind, i.InspectionDate, i.Inspector, i.Status, i.DamageCost));
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetInspection(id), ct)).RequirePermission(P.InspectionsRead);
        g.MapPost("", async (InspectionInput input, ISender s, CancellationToken ct) => { var c = await s.Send(new CreateInspection(input), ct); return Results.Created($"{Base}/inspections/{c.Id}", c); }).RequirePermission(P.InspectionsManage);
        g.MapPut("/{id:guid}", (Guid id, InspectionInput input, ISender s, CancellationToken ct) => s.Send(new UpdateInspection(id, input), ct)).RequirePermission(P.InspectionsManage);
        g.MapPost("/{id:guid}/complete", (Guid id, CompleteInspectionBody body, ISender s, CancellationToken ct) => s.Send(new CompleteInspection(id, body.RaiseWorkOrders), ct)).RequirePermission(P.InspectionsManage);
    }

    private static void MapRenewals(RouteGroupBuilder g)
    {
        g.MapGet("", (int? days, DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetRenewalWorkbench(days ?? 120, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct)).RequirePermission(P.LeasesRenew);
        g.MapPost("/drafts", (RenewalDraftsBody body, ISender s, CancellationToken ct) => s.Send(new CreateRenewalDrafts(body.LeaseIds, body.UpliftPercent), ct)).RequirePermission(P.LeasesRenew);
    }

    private static void MapWorkOrders(RouteGroupBuilder g)
    {
        g.MapPagedList<WorkOrder, WorkOrderDto>("", P.WorkOrdersRead, Docs.WorkOrder, WorkOrderDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetWorkOrder(id), ct)).RequirePermission(P.WorkOrdersRead);
        g.MapPost("", async (WorkOrderInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateWorkOrder(input), ct);
            return Results.Created($"{Base}/work-orders/{created.Id}", created);
        }).RequirePermission(P.WorkOrdersCreate);
        g.MapPut("/{id:guid}", (Guid id, WorkOrderInput input, ISender s, CancellationToken ct) => s.Send(new UpdateWorkOrder(id, input), ct)).RequirePermission(P.WorkOrdersEdit);
        g.MapPost("/{id:guid}/status", (Guid id, WorkOrderStatusBody body, ISender s, CancellationToken ct) =>
            s.Send(new ChangeWorkOrderStatus(id, body.Status, body.ActualCost, body.ResolutionNotes), ct)).RequireAuthorization(); // permission depends on the target status
    }

    private static void MapDeals(RouteGroupBuilder g)
    {
        g.MapPagedList<BrokerageDeal, DealDto>("", P.DealsRead, Docs.Deal, DealDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetDeal(id), ct)).RequirePermission(P.DealsRead);
        g.MapPost("", async (DealInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateDeal(input), ct);
            return Results.Created($"{Base}/deals/{created.Id}", created);
        }).RequirePermission(P.DealsCreate);
        g.MapPut("/{id:guid}", (Guid id, DealInput input, ISender s, CancellationToken ct) => s.Send(new UpdateDeal(id, input), ct)).RequirePermission(P.DealsEdit);
        g.MapPost("/{id:guid}/invoice-commission", (Guid id, ISender s, CancellationToken ct) => s.Send(new InvoiceDealCommission(id), ct)).RequirePermission(P.DealsInvoice);
        g.MapPost("/{id:guid}/pay-agent", (Guid id, PayAgentBody b, ISender s, CancellationToken ct) => s.Send(new PayDealAgent(id, b.BankAccountId, b.Date), ct)).RequirePermission(P.DealsPayAgent);
        g.MapPost("/{id:guid}/stage", (Guid id, DealStageBody body, ISender s, CancellationToken ct) => s.Send(new MoveDealStage(id, body.Stage, body.LostReason), ct)).RequireAuthorization();
    }
}
