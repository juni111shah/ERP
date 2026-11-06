using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Leaves out the region, community and plot number: they say where a building is.</summary>
internal sealed class PropertiesAssistantReader(IReadRepository<Property> properties) : IAssistantReader
{
    public string Key => "properties";
    public string Description => "Properties: code, name, type, ownership, number of units and whether active.";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.PropertiesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = properties.Query();
        if (search is not null) query = query.Where(p => p.Code.Contains(search) || p.Name.Contains(search));
        return await query.OrderBy(p => p.Code).Take(take).Select(p => new { p.Code, p.Name, Type = p.PropertyType.ToString(), Ownership = p.OwnershipType.ToString(), p.TotalUnits, p.IsActive, Link = "/real-estate/properties/" + p.Id }).ToListAsync(ct);
    }
}

internal sealed class UnitsAssistantReader(IReadRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "property_units";
    public string Description => "Lettable units: number, property, usage, floor, area, annual asking rent and status.";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.UnitsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = units.Query();
        if (search is not null) query = query.Where(u => u.UnitNumber.Contains(search) || properties.Query(null).Any(p => p.Id == u.PropertyId && p.Name.Contains(search)));
        return await query.OrderBy(u => u.UnitNumber).Take(take).Select(u => new { u.UnitNumber, Property = properties.Query(null).Where(p => p.Id == u.PropertyId).Select(p => p.Name).FirstOrDefault(), Usage = u.Usage.ToString(), u.Floor, u.AreaSqft, Currency = entities.Query(null).Where(e => e.Id == u.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), u.AskingRent, Status = u.Status.ToString(), Link = "/real-estate/units" }).ToListAsync(ct);
    }
}

internal sealed class LeasesAssistantReader(IReadRepository<Lease> leases, IReadRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<Party> parties) : IAssistantReader
{
    public string Key => "leases";
    public string Description => "Leases, newest first: number, tenant name, property, unit, dates, annual rent and status.";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.LeasesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = leases.Query();
        if (search is not null) query = query.Where(l => (l.Number != null && l.Number.Contains(search)) || parties.Query(null).Any(p => p.Id == l.LesseePartyId && p.Name.Contains(search)) || properties.Query(null).Any(p => p.Id == l.PropertyId && p.Name.Contains(search)));
        return await query.OrderByDescending(l => l.StartDate).Take(take).Select(l => new { l.Number, Tenant = parties.Query(null).Where(p => p.Id == l.LesseePartyId).Select(p => p.Name).FirstOrDefault(), Property = properties.Query(null).Where(p => p.Id == l.PropertyId).Select(p => p.Name).FirstOrDefault(), Unit = units.Query(null).Where(u => u.Id == l.UnitId).Select(u => u.UnitNumber).FirstOrDefault(), l.StartDate, l.EndDate, l.CurrencyCode, l.AnnualRent, Status = l.Status.ToString(), Link = "/real-estate/leases/" + l.Id }).ToListAsync(ct);
    }
}

/// <summary>Leaves out the issue, details and reported-by text: a tenant types them freely, so they can hold contact details.</summary>
internal sealed class PropertyWorkOrdersAssistantReader(IReadRepository<WorkOrder> orders, IReadRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "property_work_orders";
    public string Description => "Maintenance work orders on properties, newest first: number, property, unit, category, priority, status, dates and cost.";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.WorkOrdersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = orders.Query();
        if (search is not null) query = query.Where(w => (w.Number != null && w.Number.Contains(search)) || (w.Category != null && w.Category.Contains(search)) || properties.Query(null).Any(p => p.Id == w.PropertyId && p.Name.Contains(search)));
        return await query.OrderByDescending(w => w.ReportedOn).Take(take).Select(w => new { w.Number, Property = properties.Query(null).Where(p => p.Id == w.PropertyId).Select(p => p.Name).FirstOrDefault(), Unit = units.Query(null).Where(u => u.Id == w.UnitId).Select(u => u.UnitNumber).FirstOrDefault(), w.Category, Priority = w.Priority.ToString(), Status = w.Status.ToString(), w.ReportedOn, w.ScheduledFor, w.CompletedOn, Currency = entities.Query(null).Where(e => e.Id == w.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), w.EstimatedCost, w.ActualCost, Link = "/real-estate/work-orders" }).ToListAsync(ct);
    }
}

internal static class RealEstateAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Add a property and its units", "Go to Real estate > Properties, press New property and press Save. Then go to Real estate > Units and press New unit, or Add many to create a block floor by floor.", "/real-estate/properties", P.PropertiesRead),
        new("Create a lease", "Go to Real estate > Leases and press New lease. Choose the unit and tenant, enter the dates and rent, and press Save. Turn on Activate now, or open the draft later and press Activate.", "/real-estate/leases", P.LeasesRead),
        new("Renew or end a lease", "Open an active lease from Real estate > Leases and press Renew or Terminate early. Real estate > Lease renewals lists leases ending soon, and Create draft renewals handles the ones you tick.", "/real-estate/leases", P.LeasesRead),
        new("Bill the rent that is due", "Go to Real estate > Rent billing, choose the date to bill up to and press Run billing. It issues one invoice per instalment due, and Open invoices takes you to them.", "/real-estate/billing", P.BillingRun),
        new("Raise and track a maintenance work order", "Go to Real estate > Work orders and press New work order. Open it to move it on with Assign, Start work, Put on hold or Complete.", "/real-estate/work-orders", P.WorkOrdersRead),
    ];
}
