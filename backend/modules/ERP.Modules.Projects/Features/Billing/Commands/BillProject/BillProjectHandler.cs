using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class BillProjectHandler(
    BillingPlanner planner,
    IProjectTimesheetRepository timesheets,
    IProjectMilestoneRepository milestones,
    IProjectExpenseRepository expenses,
    IProjectProgressBillingRepository progressRows,
    ITaxCodeRepository taxCodes,
    ISender sender,
    IPostingService posting,
    IUnitOfWork uow)
    : ICommandHandler<BillProjectCommand, BillProjectResultDto>
{
    public async Task<BillProjectResultDto> Handle(BillProjectCommand command, CancellationToken ct)
    {
        // 1. What goes on the invoice, taxed with the project's tax code or the default one; a discount is its own line.
        var plan = await planner.PlanAsync(command.Input, ct);
        var p = plan.Project; var i = command.Input;
        var taxId = p.TaxCodeId ?? (await taxCodes.FindActiveDefaultAsync(p.LegalEntityId, ct))?.Id;
        var invoiceLines = plan.Lines.Select(l => new InvoiceLineInput(l.Description, l.Quantity, l.Price, TaxCodeId: taxId)).ToList();
        if (plan.Discount > 0) invoiceLines.Add(new InvoiceLineInput($"{p.Number}: discount {i.DiscountPercent:0.##}%", 1, -plan.Discount, TaxCodeId: taxId));

        return await uow.InTransactionAsync(async transactionCt =>
        {
            // 2. Raise the invoice.
            var notes = string.IsNullOrWhiteSpace(i.Notes) ? $"Generated for Project {p.Number}: {p.Name}" : $"{i.Notes.Trim()} (Project {p.Number}: {p.Name})";
            var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
                LegalEntityId: p.LegalEntityId, PartyId: p.CustomerId!.Value, InvoiceDate: DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), DueDate: i.DueDate, Lines: invoiceLines,
                Notes: notes, SourceType: "projects.project", SourceId: p.Id)), transactionCt);

            // 3. Mark the work as billed on that invoice.
            foreach (var ts in plan.Time) { ts.IsBilled = true; ts.InvoiceId = invoice.Id; }
            foreach (var e in plan.Expenses) { e.IsBilled = true; e.InvoiceId = invoice.Id; }
            foreach (var ms in plan.Milestones)
            {
                ProjectsTransitions.Milestone.Ensure(ms.Status, MilestoneStatus.Invoiced, isSystem: true);
                ms.Status = MilestoneStatus.Invoiced; ms.InvoiceId = invoice.Id;
            }

            // 4. cost that was moved into work in progress goes to cost of sales now that the work is billed
            var releaseTime = plan.Time.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
            var releaseCosts = plan.Expenses.Where(e => e.CostJournalId != null && e.WipReleaseJournalId == null).ToList();
            var journal = await ProjectWip.ReleaseAsync(posting, p, releaseTime.Sum(t => t.CostAmount) + releaseCosts.Sum(e => e.Amount), invoice.Number, $"Project {p.Number} billed, work in progress to cost of sales", transactionCt);
            if (journal is not null)
            {
                foreach (var t in releaseTime) t.WipReleaseJournalId = journal.Id;
                foreach (var e in releaseCosts) e.WipReleaseJournalId = journal.Id;
            }

            // 5. A fixed-fee percentage is remembered so it counts against the fee; save everything.
            if (plan.ProgressAmount > 0)
                progressRows.Add(new ProjectProgressBilling { ProjectId = p.Id, Date = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), Percent = i.ProgressPercent ?? 0m, Amount = plan.ProgressAmount, InvoiceId = invoice.Id });
            await timesheets.SaveChangesAsync(transactionCt);
            await expenses.SaveChangesAsync(transactionCt);
            await milestones.SaveChangesAsync(transactionCt);
            await progressRows.SaveChangesAsync(transactionCt);
            return new BillProjectResultDto(p.Id, plan.Time.Count, plan.Milestones.Count, plan.Subtotal - plan.Discount, invoice.Id, invoice.Number, plan.Expenses.Count);
        }, ct);
    }
}
