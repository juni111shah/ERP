using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Features;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Features;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Features;
using Xunit;

namespace ERP.UnitTests;

public class StatusTransitionTests
{
    [Fact]
    public void SalesQuote_transitions_enforce_valid_states()
    {
        var t = SalesTransitions.Quote;

        // Legal transitions
        t.Ensure(SalesQuoteStatus.Draft, SalesQuoteStatus.Sent);
        t.Ensure(SalesQuoteStatus.Draft, SalesQuoteStatus.Accepted);
        t.Ensure(SalesQuoteStatus.Sent, SalesQuoteStatus.Accepted);
        t.Ensure(SalesQuoteStatus.Accepted, SalesQuoteStatus.Converted, isSystem: true);
        t.Ensure(SalesQuoteStatus.Declined, SalesQuoteStatus.Draft);

        // System-only status via status endpoint throws 409
        var exSystem = Assert.Throws<ConflictException>(() =>
            t.Ensure(SalesQuoteStatus.Accepted, SalesQuoteStatus.Converted, isSystem: false));
        Assert.Contains("system conversion process", exSystem.Message);

        // Illegal transition throws 409 with allowed list
        var exIllegal = Assert.Throws<ConflictException>(() =>
            t.Ensure(SalesQuoteStatus.Converted, SalesQuoteStatus.Draft));
        Assert.Contains("Cannot transition from 'Converted' to 'Draft'", exIllegal.Message);
        Assert.Contains("Allowed transitions from 'Converted': none", exIllegal.Message);
    }

    [Fact]
    public void SalesOrder_transitions_enforce_valid_states()
    {
        var t = SalesTransitions.Order;

        // Legal transitions
        t.Ensure(SalesOrderStatus.Draft, SalesOrderStatus.Confirmed);
        t.Ensure(SalesOrderStatus.Confirmed, SalesOrderStatus.Processing);
        t.Ensure(SalesOrderStatus.Processing, SalesOrderStatus.Delivered);
        t.Ensure(SalesOrderStatus.Delivered, SalesOrderStatus.Invoiced, isSystem: true);

        // Cancelled only before delivered
        t.Ensure(SalesOrderStatus.Draft, SalesOrderStatus.Cancelled);
        t.Ensure(SalesOrderStatus.Confirmed, SalesOrderStatus.Cancelled);
        t.Ensure(SalesOrderStatus.Processing, SalesOrderStatus.Cancelled);

        var exDeliveredCancel = Assert.Throws<ConflictException>(() =>
            t.Ensure(SalesOrderStatus.Delivered, SalesOrderStatus.Cancelled));
        Assert.Contains("Cannot transition from 'Delivered' to 'Cancelled'", exDeliveredCancel.Message);

        // Setting Invoiced via status endpoint throws 409
        var exInvoiced = Assert.Throws<ConflictException>(() =>
            t.Ensure(SalesOrderStatus.Delivered, SalesOrderStatus.Invoiced, isSystem: false));
        Assert.Contains("system conversion process", exInvoiced.Message);

        // Illegal transition: Draft to Delivered
        var exIllegal = Assert.Throws<ConflictException>(() =>
            t.Ensure(SalesOrderStatus.Draft, SalesOrderStatus.Delivered));
        Assert.Contains("Allowed transitions from 'Draft': Confirmed, Cancelled", exIllegal.Message);
    }

    [Fact]
    public void PurchaseOrder_transitions_enforce_valid_states()
    {
        var t = PurchasingTransitions.Order;

        // Legal transitions
        t.Ensure(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Approved);
        t.Ensure(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted);
        t.Ensure(PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Approved);
        t.Ensure(PurchaseOrderStatus.Approved, PurchaseOrderStatus.Received);
        t.Ensure(PurchaseOrderStatus.Received, PurchaseOrderStatus.Billed, isSystem: true);

        // Cancelled before receipt
        t.Ensure(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Cancelled);
        t.Ensure(PurchaseOrderStatus.Approved, PurchaseOrderStatus.Cancelled);

        // Cancel after receipt not allowed
        var exReceivedCancel = Assert.Throws<ConflictException>(() =>
            t.Ensure(PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled));
        Assert.Contains("Cannot transition from 'Received' to 'Cancelled'", exReceivedCancel.Message);

        // Setting Billed via status endpoint throws 409
        var exBilled = Assert.Throws<ConflictException>(() =>
            t.Ensure(PurchaseOrderStatus.Received, PurchaseOrderStatus.Billed, isSystem: false));
        Assert.Contains("system conversion process", exBilled.Message);
    }

    [Fact]
    public void PurchaseRequisition_transitions_enforce_valid_states()
    {
        var t = PurchasingTransitions.Requisition;

        // Legal transitions
        t.Ensure(PurchaseRequisitionStatus.Draft, PurchaseRequisitionStatus.PendingApproval);
        t.Ensure(PurchaseRequisitionStatus.Draft, PurchaseRequisitionStatus.Approved);
        t.Ensure(PurchaseRequisitionStatus.PendingApproval, PurchaseRequisitionStatus.Approved);
        t.Ensure(PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Ordered, isSystem: true);
        t.Ensure(PurchaseRequisitionStatus.Rejected, PurchaseRequisitionStatus.Draft);

        // Setting Ordered via status endpoint throws 409
        var exOrdered = Assert.Throws<ConflictException>(() =>
            t.Ensure(PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Ordered, isSystem: false));
        Assert.Contains("system conversion process", exOrdered.Message);

        // Illegal transition
        var exIllegal = Assert.Throws<ConflictException>(() =>
            t.Ensure(PurchaseRequisitionStatus.Ordered, PurchaseRequisitionStatus.Draft));
        Assert.Contains("Cannot transition from 'Ordered' to 'Draft'", exIllegal.Message);
    }

    [Fact]
    public void OpportunityStage_transitions_enforce_pipeline_order()
    {
        var t = CrmTransitions.Opportunity;

        // Legal transitions
        t.Ensure(OpportunityStage.Qualification, OpportunityStage.Proposal);
        t.Ensure(OpportunityStage.Proposal, OpportunityStage.Negotiation);
        t.Ensure(OpportunityStage.Negotiation, OpportunityStage.ClosedWon);
        t.Ensure(OpportunityStage.Negotiation, OpportunityStage.ClosedLost);
        t.Ensure(OpportunityStage.ClosedLost, OpportunityStage.Qualification);

        // Illegal transition: Qualification directly to ClosedWon
        var exIllegal = Assert.Throws<ConflictException>(() =>
            t.Ensure(OpportunityStage.Qualification, OpportunityStage.ClosedWon));
        Assert.Contains("Allowed transitions from 'Qualification': Proposal, ClosedLost", exIllegal.Message);

        // ClosedWon is terminal
        var exWon = Assert.Throws<ConflictException>(() =>
            t.Ensure(OpportunityStage.ClosedWon, OpportunityStage.Qualification));
        Assert.Contains("Allowed transitions from 'ClosedWon': none", exWon.Message);
    }

    [Fact]
    public void Services_transitions_enforce_valid_states()
    {
        // Milestone
        var ms = ServicesTransitions.Milestone;
        ms.Ensure(ERP.Modules.Services.Domain.MilestoneStatus.Planned, ERP.Modules.Services.Domain.MilestoneStatus.ReadyToInvoice);
        ms.Ensure(ERP.Modules.Services.Domain.MilestoneStatus.ReadyToInvoice, ERP.Modules.Services.Domain.MilestoneStatus.Invoiced, isSystem: true);
        ms.Ensure(ERP.Modules.Services.Domain.MilestoneStatus.Invoiced, ERP.Modules.Services.Domain.MilestoneStatus.Paid, isSystem: true);

        var exMsInvoiced = Assert.Throws<ConflictException>(() =>
            ms.Ensure(ERP.Modules.Services.Domain.MilestoneStatus.ReadyToInvoice, ERP.Modules.Services.Domain.MilestoneStatus.Invoiced, isSystem: false));
        Assert.Contains("system conversion process", exMsInvoiced.Message);

        // Engagement
        var eng = ServicesTransitions.Engagement;
        eng.Ensure(EngagementStatus.Draft, EngagementStatus.Active);
        eng.Ensure(EngagementStatus.Active, EngagementStatus.Completed);
        var exEng = Assert.Throws<ConflictException>(() =>
            eng.Ensure(EngagementStatus.Completed, EngagementStatus.Active));
        Assert.Contains("Allowed transitions from 'Completed': none", exEng.Message);

        // Retainer
        var ret = ServicesTransitions.Retainer;
        ret.Ensure(RetainerStatus.Active, RetainerStatus.Paused);
        ret.Ensure(RetainerStatus.Paused, RetainerStatus.Terminated);
        var exRet = Assert.Throws<ConflictException>(() =>
            ret.Ensure(RetainerStatus.Terminated, RetainerStatus.Active));
        Assert.Contains("Allowed transitions from 'Terminated': none", exRet.Message);
    }

    [Fact]
    public void Projects_transitions_enforce_valid_states()
    {
        // Project
        var prj = ProjectsTransitions.Project;
        prj.Ensure(ProjectStatus.Planning, ProjectStatus.Active);
        prj.Ensure(ProjectStatus.Active, ProjectStatus.Completed);
        var exPrj = Assert.Throws<ConflictException>(() =>
            prj.Ensure(ProjectStatus.Completed, ProjectStatus.Active));
        Assert.Contains("Allowed transitions from 'Completed': none", exPrj.Message);

        // Project Milestone
        var ms = ProjectsTransitions.Milestone;
        ms.Ensure(ERP.Modules.Projects.Domain.MilestoneStatus.Pending, ERP.Modules.Projects.Domain.MilestoneStatus.Achieved);
        ms.Ensure(ERP.Modules.Projects.Domain.MilestoneStatus.Achieved, ERP.Modules.Projects.Domain.MilestoneStatus.Invoiced, isSystem: true);
        var exMs = Assert.Throws<ConflictException>(() =>
            ms.Ensure(ERP.Modules.Projects.Domain.MilestoneStatus.Achieved, ERP.Modules.Projects.Domain.MilestoneStatus.Invoiced, isSystem: false));
        Assert.Contains("system conversion process", exMs.Message);

        // Task
        var task = ProjectsTransitions.Task;
        task.Ensure(ProjectTaskStatus.Todo, ProjectTaskStatus.InProgress);
        task.Ensure(ProjectTaskStatus.InProgress, ProjectTaskStatus.Review);
        task.Ensure(ProjectTaskStatus.Review, ProjectTaskStatus.Done);
        var exTask = Assert.Throws<ConflictException>(() =>
            task.Ensure(ProjectTaskStatus.Todo, ProjectTaskStatus.Done));
        Assert.Contains("Allowed transitions from 'Todo': InProgress", exTask.Message);
    }

    [Fact]
    public void Manufacturing_workorder_transitions_enforce_valid_states()
    {
        var wo = ManufacturingTransitions.WorkOrder;
        wo.Ensure(WorkOrderStatus.Draft, WorkOrderStatus.Planned);
        wo.Ensure(WorkOrderStatus.Planned, WorkOrderStatus.InProgress);
        wo.Ensure(WorkOrderStatus.InProgress, WorkOrderStatus.Completed);

        var exWo = Assert.Throws<ConflictException>(() =>
            wo.Ensure(WorkOrderStatus.Completed, WorkOrderStatus.InProgress));
        Assert.Contains("Allowed transitions from 'Completed': none", exWo.Message);
    }
}
