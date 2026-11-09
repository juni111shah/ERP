using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

internal sealed class InvoiceMilestoneHandler(
    IBillingMilestoneRepository milestones,
    IServiceEngagementRepository engagements,
    ISender sender)
    : ICommandHandler<InvoiceMilestoneCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(InvoiceMilestoneCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the milestone; only the system moves it to invoiced.
        var milestone = await milestones.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Billing milestone {request.Id} not found.");

        ServicesTransitions.Milestone.Ensure(milestone.Status, MilestoneStatus.Invoiced, isSystem: true);

        // 2. The engagement must be live and have a client to bill.
        var engagement = await engagements.FindAsync(milestone.EngagementId, cancellationToken)
            ?? throw new NotFoundException($"Service engagement {milestone.EngagementId} not found.");

        if (engagement.Status == EngagementStatus.Cancelled)
        {
            throw new ConflictException("Cannot invoice milestone for a cancelled service engagement.");
        }

        if (engagement.ClientId == Guid.Empty)
        {
            throw new ConflictException("Service engagement must have a valid client to generate an invoice.");
        }

        // 3. Raise the draft invoice, due no earlier than today.
        var invoiceDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var dueDate = milestone.DueDate > invoiceDate ? milestone.DueDate : invoiceDate;

        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: milestone.LegalEntityId,
            PartyId: engagement.ClientId,
            InvoiceDate: invoiceDate,
            DueDate: dueDate,
            Lines: [
                new InvoiceLineInput(
                    $"{milestone.MilestoneCode}: {milestone.Title} ({engagement.Title})",
                    1,
                    milestone.Amount)
            ],
            Notes: $"Milestone billing for {engagement.EngagementCode}: {engagement.Title}",
            SourceType: MilestonePaymentNotificationHandler.SourceType,
            SourceId: milestone.Id)), cancellationToken);

        // 4. Mark the milestone invoiced and save.
        milestone.Status = MilestoneStatus.Invoiced;
        milestone.InvoiceId = invoice.Id;
        milestone.CompletionDate ??= invoiceDate;

        await milestones.SaveChangesAsync(cancellationToken);
        return invoice;
    }
}
