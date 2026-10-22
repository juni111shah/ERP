using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

public sealed record ApprovalInput(string Subject, string? Justification, decimal? Amount, Guid? LegalEntityId);

public sealed record ApprovalDto(
    Guid Id, Guid? LegalEntityId, string DocumentType, Guid? DocumentId, string Subject, string? Justification, decimal? Amount, string? CurrencyCode,
    Guid RequestedBy, DateTime RequestedAt, ApprovalStatus Status, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionComment)
{
    public static ApprovalDto From(ApprovalRequest r) => new(
        r.Id, r.LegalEntityId, r.DocumentType, r.DocumentId, r.Subject, r.Justification, r.Amount, r.CurrencyCode,
        r.RequestedBy, r.RequestedAt, r.Status, r.DecidedBy, r.DecidedAt, r.DecisionComment);
}

/// <summary>"mine" = requests I made; otherwise approvers see everyone else's pending requests and other users only their own.</summary>
public sealed record ListApprovals(string? Scope) : IQuery<IReadOnlyList<ApprovalDto>>;
public sealed record SubmitApproval(ApprovalInput Input) : ICommand<ApprovalDto>;
public sealed record DecideApproval(Guid Id, bool Approve, string? Comment) : ICommand<ApprovalDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ApprovalsDecide;
}
public sealed record WithdrawApproval(Guid Id) : ICommand<ApprovalDto>;

public sealed class SubmitApprovalValidator : AbstractValidator<SubmitApproval>
{
    public SubmitApprovalValidator() => RuleFor(x => x.Input.Subject).NotEmpty().MaximumLength(300).WithMessage("Subject is required.");
}

internal sealed class ApprovalHandlers(IReadRepository<ApprovalRequest> requests, IApprovalService approvals, ICurrentUser user) :
    IRequestHandler<ListApprovals, IReadOnlyList<ApprovalDto>>,
    IRequestHandler<SubmitApproval, ApprovalDto>,
    IRequestHandler<DecideApproval, ApprovalDto>,
    IRequestHandler<WithdrawApproval, ApprovalDto>
{
    public async Task<IReadOnlyList<ApprovalDto>> Handle(ListApprovals q, CancellationToken ct)
    {
        var me = user.UserId;
        var query = requests.Query();
        query = q.Scope == "mine" ? query.Where(r => r.RequestedBy == me)
            : user.HasPermission(CorePermissions.ApprovalsDecide) ? query.Where(r => r.Status == ApprovalStatus.Pending && r.RequestedBy != me)
            : query.Where(r => r.RequestedBy == me);
        return (await query.OrderByDescending(r => r.RequestedAt).Take(200).ToListAsync(ct)).Select(ApprovalDto.From).ToList();
    }

    public async Task<ApprovalDto> Handle(SubmitApproval cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        return ApprovalDto.From(await approvals.SubmitAsync("general", null, i.Subject, i.Amount, i.Justification, i.LegalEntityId, ct));
    }

    public async Task<ApprovalDto> Handle(DecideApproval cmd, CancellationToken ct) => ApprovalDto.From(await approvals.DecideAsync(cmd.Id, cmd.Approve, cmd.Comment, ct));

    public async Task<ApprovalDto> Handle(WithdrawApproval cmd, CancellationToken ct) => ApprovalDto.From(await approvals.WithdrawAsync(cmd.Id, ct));
}
