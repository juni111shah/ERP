using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;

namespace ERP.Application.Workflow;

public sealed record ApprovalPolicyInput(string DocumentType, decimal? MinAmount, bool IsActive = true);
public sealed record ApprovalPolicyDto(Guid Id, string DocumentType, decimal? MinAmount, string ApproverPermission, bool IsActive);

/// <summary>Rules for when a document needs a second person's approval before it is posted (for example bills of 5,000 or more).</summary>
public sealed record ListApprovalPolicies : IQuery<IReadOnlyList<ApprovalPolicyDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
public sealed record SaveApprovalPolicy(Guid? Id, ApprovalPolicyInput Input) : ICommand<ApprovalPolicyDto>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
public sealed record DeleteApprovalPolicy(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }

public static class ApprovalDocumentTypes
{
    /// <summary>Document types whose posting honours approval policies.</summary>
    public static readonly string[] All = ["bill"];
}

public sealed class SaveApprovalPolicyValidator : AbstractValidator<SaveApprovalPolicy>
{
    public SaveApprovalPolicyValidator()
    {
        RuleFor(x => x.Input.DocumentType).Must(t => ApprovalDocumentTypes.All.Contains(t)).WithMessage($"Approval rules are supported for: {string.Join(", ", ApprovalDocumentTypes.All)}.");
        RuleFor(x => x.Input.MinAmount).GreaterThanOrEqualTo(0).When(x => x.Input.MinAmount is not null);
    }
}

internal sealed class ApprovalPolicyHandlers(IRepository<ApprovalPolicy> policies) :
    IRequestHandler<ListApprovalPolicies, IReadOnlyList<ApprovalPolicyDto>>,
    IRequestHandler<SaveApprovalPolicy, ApprovalPolicyDto>,
    IRequestHandler<DeleteApprovalPolicy, Unit>
{
    public async Task<IReadOnlyList<ApprovalPolicyDto>> Handle(ListApprovalPolicies q, CancellationToken ct)
        => (await policies.ListAsync(null, ct)).Select(ToDto).OrderBy(p => p.DocumentType).ThenBy(p => p.MinAmount).ToList();

    public async Task<ApprovalPolicyDto> Handle(SaveApprovalPolicy cmd, CancellationToken ct)
    {
        var policy = cmd.Id is { } id ? await policies.GetForUpdateAsync(id, ct) : new ApprovalPolicy();
        policy.DocumentType = cmd.Input.DocumentType; policy.MinAmount = cmd.Input.MinAmount; policy.IsActive = cmd.Input.IsActive;
        if (cmd.Id is null) policies.Add(policy);
        await policies.SaveChangesAsync(ct);
        return ToDto(policy);
    }

    public async Task<Unit> Handle(DeleteApprovalPolicy cmd, CancellationToken ct)
    {
        policies.Remove(await policies.GetForUpdateAsync(cmd.Id, ct));
        await policies.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static ApprovalPolicyDto ToDto(ApprovalPolicy p) => new(p.Id, p.DocumentType, p.MinAmount, p.ApproverPermission, p.IsActive);
}
