using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Reminders;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record EmployeeDocumentInput(Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy, string? Notes);
public sealed record EmployeeDocumentDto(Guid Id, Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy, string? Notes, int? DaysLeft, bool IsExpired);
public sealed record EmployeeDocumentRow(Guid Id, Guid EmployeeId, EmployeeDocumentType Type, string? Number, DateOnly? IssueDate, DateOnly? ExpiryDate, string? IssuedBy);
public sealed record ExpiringDocumentDto(Guid Id, Guid EmployeeId, string EmployeeNumber, string EmployeeName, EmployeeDocumentType Type, string? Number, DateOnly ExpiryDate, int DaysLeft, bool IsExpired);

public sealed record GetEmployeeDocument(Guid Id) : IQuery<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record CreateEmployeeDocument(EmployeeDocumentInput Input) : ICommand<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record UpdateEmployeeDocument(Guid Id, EmployeeDocumentInput Input) : ICommand<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
public sealed record DeleteEmployeeDocument(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
/// <summary>Documents that expire within <c>Days</c> days or have already expired, soonest first. Staff who have left are left out.</summary>
public sealed record ListExpiringDocuments(int Days) : IQuery<IReadOnlyList<ExpiringDocumentDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }

public sealed class EmployeeDocumentInputValidator : AbstractValidator<EmployeeDocumentInput>
{
    public EmployeeDocumentInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Number).MaximumLength(100);
        RuleFor(x => x.IssuedBy).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x).Must(x => x.IssueDate is null || x.ExpiryDate is null || x.ExpiryDate >= x.IssueDate).WithName("expiryDate").WithMessage("The expiry date cannot be before the issue date.");
    }
}

public sealed class CreateEmployeeDocumentValidator : AbstractValidator<CreateEmployeeDocument> { public CreateEmployeeDocumentValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeDocumentInputValidator()); }
public sealed class UpdateEmployeeDocumentValidator : AbstractValidator<UpdateEmployeeDocument> { public UpdateEmployeeDocumentValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeDocumentInputValidator()); }

public sealed class ListExpiringDocumentsValidator : AbstractValidator<ListExpiringDocuments>
{
    public ListExpiringDocumentsValidator() => RuleFor(x => x.Days).InclusiveBetween(0, 3650);
}

/// <summary>How close a document is to expiring. The dashboard, the list and the reminders all use these same thresholds.</summary>
public static class DocumentExpiry
{
    public static readonly int[] ReminderDays = [60, 30, 7];

    public static int? DaysLeft(DateOnly? expiry, DateOnly today) => expiry is { } e ? e.DayNumber - today.DayNumber : null;

    /// <summary>The reminder stage a document is in: 60, 30 or 7 (the smallest threshold that still covers the days left); null when it is further away or already expired.</summary>
    public static int? ReminderStage(int daysLeft) => daysLeft < 0 ? null : ReminderDays.Where(d => daysLeft <= d).Cast<int?>().Min();
}

internal sealed class EmployeeDocumentHandlers(IRepository<EmployeeDocument> documents, IReadRepository<Employee> employees, ILegalEntityGuard guard, IClock clock) :
    IRequestHandler<GetEmployeeDocument, EmployeeDocumentDto>,
    IRequestHandler<CreateEmployeeDocument, EmployeeDocumentDto>,
    IRequestHandler<UpdateEmployeeDocument, EmployeeDocumentDto>,
    IRequestHandler<DeleteEmployeeDocument, Unit>,
    IRequestHandler<ListExpiringDocuments, IReadOnlyList<ExpiringDocumentDto>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<EmployeeDocumentDto> Handle(GetEmployeeDocument q, CancellationToken ct) => ToDto(await documents.GetAsync(q.Id, ct), Today);

    public async Task<EmployeeDocumentDto> Handle(CreateEmployeeDocument cmd, CancellationToken ct)
    {
        var employee = await employees.GetAsync(cmd.Input.EmployeeId, ct);
        await guard.RequireAsync(employee.LegalEntityId, ct);
        var d = new EmployeeDocument { EmployeeId = employee.Id, LegalEntityId = employee.LegalEntityId };
        Map(cmd.Input, d);
        documents.Add(d);
        await documents.SaveChangesAsync(ct);
        return ToDto(d, Today);
    }

    public async Task<EmployeeDocumentDto> Handle(UpdateEmployeeDocument cmd, CancellationToken ct)
    {
        var d = await documents.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Input.EmployeeId != d.EmployeeId) throw new FieldValidationException("employeeId", "A document cannot move to another employee.");
        Map(cmd.Input, d);
        await documents.SaveChangesAsync(ct);
        return ToDto(d, Today);
    }

    public async Task<Unit> Handle(DeleteEmployeeDocument cmd, CancellationToken ct)
    {
        documents.Remove(await documents.GetForUpdateAsync(cmd.Id, ct));
        await documents.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<IReadOnlyList<ExpiringDocumentDto>> Handle(ListExpiringDocuments q, CancellationToken ct)
    {
        var today = Today;
        var limit = today.AddDays(q.Days);
        var due = await documents.Query(new Specification<EmployeeDocument>(d => d.ExpiryDate != null && d.ExpiryDate <= limit)).ToListAsync(ct);
        var ids = due.Select(d => d.EmployeeId).Distinct().ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id) && e.Status != EmployeeStatus.Terminated)).Select(e => new { e.Id, e.Number, e.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id);
        return due.Where(d => staff.ContainsKey(d.EmployeeId)).OrderBy(d => d.ExpiryDate).ThenBy(d => staff[d.EmployeeId].Number)
            .Select(d => new ExpiringDocumentDto(d.Id, d.EmployeeId, staff[d.EmployeeId].Number, staff[d.EmployeeId].FullName, d.Type, d.Number, d.ExpiryDate!.Value, d.ExpiryDate.Value.DayNumber - today.DayNumber, d.ExpiryDate.Value < today)).ToList();
    }

    private static void Map(EmployeeDocumentInput i, EmployeeDocument d)
    {
        d.Type = i.Type; d.Number = i.Number?.Trim(); d.IssueDate = i.IssueDate; d.ExpiryDate = i.ExpiryDate;
        d.IssuedBy = string.IsNullOrWhiteSpace(i.IssuedBy) ? null : i.IssuedBy.Trim(); d.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        if (string.IsNullOrWhiteSpace(d.Number)) d.Number = null;
    }

    private static EmployeeDocumentDto ToDto(EmployeeDocument d, DateOnly today)
    {
        var left = DocumentExpiry.DaysLeft(d.ExpiryDate, today);
        return new(d.Id, d.EmployeeId, d.Type, d.Number, d.IssueDate, d.ExpiryDate, d.IssuedBy, d.Notes, left, left < 0);
    }
}

/// <summary>Reminds HR managers once at 60, 30 and 7 days before a document expires. Reads the data directly: jobs run without a signed-in user.</summary>
public sealed class EmployeeDocumentReminders(IReadRepository<EmployeeDocument> documents, IReadRepository<Employee> employees) : IReminderSource
{
    public string? ModuleKey => HrModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var limit = today.AddDays(DocumentExpiry.ReminderDays.Max());
        var due = await documents.Query(new Specification<EmployeeDocument>(d => d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= limit)).ToListAsync(ct);
        if (due.Count == 0) return [];
        var ids = due.Select(d => d.EmployeeId).Distinct().ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id) && e.Status != EmployeeStatus.Terminated)).Select(e => new { e.Id, e.Number, e.FullName }).ToListAsync(ct)).ToDictionary(e => e.Id);
        var list = new List<ReminderCandidate>();
        foreach (var d in due)
        {
            if (!staff.TryGetValue(d.EmployeeId, out var e)) continue;
            var left = d.ExpiryDate!.Value.DayNumber - today.DayNumber;
            if (DocumentExpiry.ReminderStage(left) is not { } stage) continue;
            list.Add(new ReminderCandidate($"hr-doc-expiry:{d.Id}:{d.ExpiryDate:yyyyMMdd}:{stage}", $"{d.Type} expires in {left} day(s): {e.Number} {e.FullName}",
                $"{d.Type}{(d.Number is null ? "" : " " + d.Number)} of {e.FullName} expires on {d.ExpiryDate:dd MMM yyyy}.", "/hr/employees", P.EmployeesManage));
        }
        return list;
    }
}
