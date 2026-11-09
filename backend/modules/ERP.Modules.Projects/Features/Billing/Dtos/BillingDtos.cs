using ERP.Domain.Finance;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record ProjectBillingSummaryDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    ProjectBillingType BillingType,
    decimal UnbilledHours,
    decimal UnbilledHoursAmount,
    int AchievedMilestonesCount,
    decimal AchievedMilestonesAmount,
    decimal TotalUnbilledAmount,
    decimal UnbilledExpensesAmount = 0m,
    decimal AwaitingApprovalAmount = 0m,
    decimal FixedFeeRemaining = 0m);

public sealed record BillProjectResultDto(
    Guid ProjectId,
    int BilledTimesheetsCount,
    int BilledMilestonesCount,
    decimal TotalAmountBilled,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null,
    int BilledExpensesCount = 0);

public sealed record BillPreviewLineDto(string Description, decimal Quantity, decimal Price, decimal Amount);
public sealed record BillPreviewDto(Guid ProjectId, IReadOnlyList<BillPreviewLineDto> Lines, decimal Subtotal, decimal Discount, decimal Total, int Timesheets, int Milestones, int Expenses);

public sealed record BillableTimeDto(Guid Id, DateOnly Date, string Worker, string? Task, string? Description, decimal Hours, decimal Amount);
public sealed record BillableExpenseDto(Guid Id, DateOnly Date, string Category, string Description, decimal Amount);
public sealed record BillableMilestoneDto(Guid Id, string Title, DateOnly? AchievedDate, decimal Amount);
public sealed record BillableWorkDto(Guid ProjectId, ProjectBillingType BillingType, decimal Fee, decimal FixedFeeRemaining, IReadOnlyList<BillableTimeDto> Time, IReadOnlyList<BillableExpenseDto> Expenses, IReadOnlyList<BillableMilestoneDto> Milestones);
public sealed record ProjectInvoiceDto(Guid Id, string Number, DateOnly Date, DateOnly DueDate, DocumentStatus Status, decimal Total, decimal Paid);
