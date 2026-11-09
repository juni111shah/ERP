using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Checks shared by the handlers that create, edit and close projects.</summary>
public sealed class ProjectChecks(
    IReadRepository<ERP.Domain.Parties.Party> parties,
    IProjectTaskRepository tasks,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IProjectMilestoneRepository milestones)
{
    /// <summary>Checks the customer and returns its name; the project always shows the name on the customer record.</summary>
    public async Task<string?> EnsureCustomerAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id) return null;
        var party = await parties.FindAsync(id, ct) ?? throw new FieldValidationException("customerId", "Customer not found.");
        if (party.Roles != ERP.Domain.Parties.PartyRoles.None && !party.Roles.HasFlag(ERP.Domain.Parties.PartyRoles.Customer))
            throw new FieldValidationException("customerId", "Choose a customer.");
        return party.Name;
    }

    /// <summary>A project is completed only when nothing is left open or unbilled.</summary>
    public async Task EnsureCanCompleteAsync(Project p, CancellationToken ct)
    {
        var id = p.Id;
        var open = await tasks.CountOpenAsync(id, ct);
        if (open > 0) throw new ConflictException($"{open} task(s) are still open. Finish or cancel them before completing the project.");
        if (p.BillingType == ProjectBillingType.NonBillable) return;
        // a fixed-fee project bills milestones and percentages, so its hours never wait to be billed
        var unbilledTime = p.BillingType == ProjectBillingType.FixedFee ? 0 : await timesheets.CountReadyToBillAsync(id, ct);
        var unbilledCosts = await expenses.CountReadyToBillAsync(id, ct);
        var unbilledMilestones = await milestones.CountAchievedAsync(id, ct);
        if (unbilledTime + unbilledCosts + unbilledMilestones > 0)
            throw new ConflictException($"Bill or write off the remaining work first ({unbilledTime} time entries, {unbilledCosts} expenses, {unbilledMilestones} milestones).");
    }
}
