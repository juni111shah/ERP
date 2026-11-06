using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

public sealed record ApprovalInput(string Subject, string? Justification, decimal? Amount, Guid? LegalEntityId);

public sealed record ApprovalStepDto(Guid Id, int Level, string ApproverPermission, ApprovalStepStatus Status, Guid? DecidedBy, DateTime? DecidedAt, string? Comment);

/// <summary><c>CurrentLevel</c> and <c>CurrentApproverPermission</c> are null once decided. <c>Steps</c> is the full decision history, lowest level first (empty on requests from before chains existed).</summary>
public sealed record ApprovalDto(
    Guid Id, Guid? LegalEntityId, string DocumentType, Guid? DocumentId, string Subject, string? Justification, decimal? Amount, string? CurrencyCode,
    Guid RequestedBy, DateTime RequestedAt, ApprovalStatus Status, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionComment,
    int? CurrentLevel = null, string? CurrentApproverPermission = null, IReadOnlyList<ApprovalStepDto>? Steps = null)
{
    public static ApprovalDto From(ApprovalRequest r)
    {
        var current = r.CurrentStep;
        var open = r.Status == ApprovalStatus.Pending;
        return new(
            r.Id, r.LegalEntityId, r.DocumentType, r.DocumentId, r.Subject, r.Justification, r.Amount, r.CurrencyCode,
            r.RequestedBy, r.RequestedAt, r.Status, r.DecidedBy, r.DecidedAt, r.DecisionComment,
            open ? current?.Level ?? 1 : null, open ? ApprovalRouting.EffectivePermission(current?.ApproverPermission) : null,
            r.Steps.OrderBy(s => s.Level).Select(s => new ApprovalStepDto(s.Id, s.Level, s.ApproverPermission, s.Status, s.DecidedBy, s.DecidedAt, s.Comment)).ToList());
    }
}

/// <summary>"mine" = requests I made; otherwise the pending requests whose current level I may decide, and for a user who may decide nothing their own.</summary>
public sealed record ListApprovals(string? Scope) : IQuery<IReadOnlyList<ApprovalDto>>;
/// <summary>One request with its decision history, for the requester, whoever can decide one of its levels and auditors.</summary>
public sealed record GetApproval(Guid Id) : IQuery<ApprovalDto>;
public sealed record SubmitApproval(ApprovalInput Input) : ICommand<ApprovalDto>;
/// <summary>The permission of the request's current level is checked by the approval service: it differs per policy.</summary>
public sealed record DecideApproval(Guid Id, bool Approve, string? Comment) : ICommand<ApprovalDto>;
public sealed record WithdrawApproval(Guid Id) : ICommand<ApprovalDto>;

public sealed class SubmitApprovalValidator : AbstractValidator<SubmitApproval>
{
    public SubmitApprovalValidator() => RuleFor(x => x.Input.Subject).NotEmpty().MaximumLength(300).WithMessage("Subject is required.");
}

internal sealed class ApprovalHandlers(IReadRepository<ApprovalRequest> requests, IApprovalService approvals, ICurrentUser user) :
    IRequestHandler<ListApprovals, IReadOnlyList<ApprovalDto>>,
    IRequestHandler<GetApproval, ApprovalDto>,
    IRequestHandler<SubmitApproval, ApprovalDto>,
    IRequestHandler<DecideApproval, ApprovalDto>,
    IRequestHandler<WithdrawApproval, ApprovalDto>
{
    private const int MaxRows = 200;

    public async Task<IReadOnlyList<ApprovalDto>> Handle(ListApprovals q, CancellationToken ct)
    {
        var me = user.UserId;
        if (q.Scope != "mine")
        {
            var pending = await requests.Query(new Specification<ApprovalRequest>(r => r.Status == ApprovalStatus.Pending && r.RequestedBy != me).Include(r => r.Steps))
                .OrderByDescending(r => r.RequestedAt).ToListAsync(ct);
            var mayDecide = pending.Where(r => user.HasPermission(ApprovalRouting.EffectivePermission(r.CurrentStep?.ApproverPermission)) && !r.Steps.Any(s => s.DecidedBy == me)).Take(MaxRows).ToList();
            if (mayDecide.Count > 0 || user.HasPermission(CorePermissions.ApprovalsDecide)) return mayDecide.Select(ApprovalDto.From).ToList();
        }
        var mine = await requests.Query(new Specification<ApprovalRequest>(r => r.RequestedBy == me).Include(r => r.Steps)).OrderByDescending(r => r.RequestedAt).Take(MaxRows).ToListAsync(ct);
        return mine.Select(ApprovalDto.From).ToList();
    }

    public async Task<ApprovalDto> Handle(GetApproval q, CancellationToken ct)
    {
        var r = await requests.Query(new Specification<ApprovalRequest>(x => x.Id == q.Id).Include(x => x.Steps)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var canSee = r.RequestedBy == user.UserId || user.HasPermission(CorePermissions.AuditRead) || user.HasPermission(CorePermissions.ApprovalsDecide)
            || r.Steps.Any(s => user.HasPermission(ApprovalRouting.EffectivePermission(s.ApproverPermission)));
        return canSee ? ApprovalDto.From(r) : throw new NotFoundException();
    }

    public async Task<ApprovalDto> Handle(SubmitApproval cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        return ApprovalDto.From(await approvals.SubmitAsync("general", null, i.Subject, i.Amount, i.Justification, i.LegalEntityId, ct));
    }

    public async Task<ApprovalDto> Handle(DecideApproval cmd, CancellationToken ct) => ApprovalDto.From(await approvals.DecideAsync(cmd.Id, cmd.Approve, cmd.Comment, ct));

    public async Task<ApprovalDto> Handle(WithdrawApproval cmd, CancellationToken ct) => ApprovalDto.From(await approvals.WithdrawAsync(cmd.Id, ct));
}
