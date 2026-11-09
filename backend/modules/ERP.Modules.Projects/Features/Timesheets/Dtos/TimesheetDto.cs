using System.Linq.Expressions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// One entry of time. HourlyRate and CostRate may be left out: they then come from the rate cards. The worker is either a sign-in account
/// (WorkerUserId; the name is filled in) or a typed name. Id is only used by the weekly grid (<see cref="SaveTimesheetWeekCommand"/>).
/// </summary>
public sealed record TimesheetInput(
    Guid ProjectId,
    Guid? TaskId,
    DateOnly Date,
    string? WorkerName,
    decimal Hours,
    decimal? HourlyRate,
    string? Description,
    bool IsBillable = true,
    Guid? WorkerUserId = null,
    decimal? CostRate = null,
    Guid? Id = null);

public sealed record TimesheetDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? TaskId,
    string? TaskTitle,
    DateOnly Date,
    string WorkerName,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string? Description,
    bool IsBillable,
    bool IsBilled,
    Guid? InvoiceId,
    DateTime CreatedAt,
    Guid? WorkerUserId = null,
    decimal CostRate = 0m,
    decimal CostAmount = 0m,
    TimesheetStatus ApprovalStatus = TimesheetStatus.Approved,
    string? RejectReason = null,
    bool WrittenOff = false)
{
    /// <summary>The list projection (translated to SQL); names are filled in, and cost hidden, afterwards by <see cref="ProjectListEnrich.Timesheets"/>.</summary>
    public static readonly Expression<Func<ProjectTimesheet, TimesheetDto>> Projection = ts => new TimesheetDto(
        ts.Id, ts.ProjectId, "", ts.TaskId, null, ts.Date, ts.WorkerName, ts.Hours, ts.HourlyRate, ts.TotalAmount, ts.Description, ts.IsBillable, ts.IsBilled, ts.InvoiceId, ts.CreatedAt,
        ts.WorkerUserId, ts.CostRate, ts.CostAmount, ts.ApprovalStatus, ts.RejectReason, ts.WrittenOff);
}
