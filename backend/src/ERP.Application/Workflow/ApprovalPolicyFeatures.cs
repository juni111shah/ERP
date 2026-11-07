using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;

namespace ERP.Application.Workflow;

/// <summary><c>ApproverPermission</c> defaults to the core approver permission, <c>Level</c> to 1. Rows with higher levels for the same document type form a sequential chain.</summary>
public sealed record ApprovalPolicyInput(string DocumentType, decimal? MinAmount, bool IsActive = true, string? ApproverPermission = null, int? Level = null);
public sealed record ApprovalPolicyDto(Guid Id, string DocumentType, decimal? MinAmount, string ApproverPermission, bool IsActive, int Level = 1);

/// <summary>Rules for when a document needs a second person's approval before it is posted (for example bills of 5,000 or more).</summary>
public sealed record ListApprovalPolicies : IQuery<IReadOnlyList<ApprovalPolicyDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
public sealed record SaveApprovalPolicy(Guid? Id, ApprovalPolicyInput Input) : ICommand<ApprovalPolicyDto>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
public sealed record DeleteApprovalPolicy(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }

public static class ApprovalDocumentTypes
{
    public const string Bill = "bill";
    public const string Journal = "journal";
    public const string Payment = "payment";

    /// <summary>Document types whose posting honours approval policies.</summary>
    public static readonly string[] All = [Bill, Journal, Payment, "purchase_order", "purchase_requisition"];
}

public sealed class SaveApprovalPolicyValidator : AbstractValidator<SaveApprovalPolicy>
{
    public SaveApprovalPolicyValidator(IPermissionCatalog catalog)
    {
        RuleFor(x => x.Input.DocumentType).Must(t => ApprovalDocumentTypes.All.Contains(t)).WithMessage($"Approval rules are supported for: {string.Join(", ", ApprovalDocumentTypes.All)}.");
        RuleFor(x => x.Input.MinAmount).GreaterThanOrEqualTo(0).When(x => x.Input.MinAmount is not null);
        RuleFor(x => x.Input.Level).InclusiveBetween(1, 10).When(x => x.Input.Level is not null).WithMessage("The level must be between 1 and 10.");
        // A permission nobody can hold would leave every request of this policy waiting forever.
        RuleFor(x => x.Input.ApproverPermission).Must(p => catalog.All.Any(d => d.Key == p)).When(x => !string.IsNullOrWhiteSpace(x.Input.ApproverPermission)).WithMessage("Choose an existing permission for the approver.");
    }
}

internal sealed class ApprovalPolicyHandlers(IRepository<ApprovalPolicy> policies) :
    IRequestHandler<ListApprovalPolicies, IReadOnlyList<ApprovalPolicyDto>>,
    IRequestHandler<SaveApprovalPolicy, ApprovalPolicyDto>,
    IRequestHandler<DeleteApprovalPolicy, Unit>
{
    public async Task<IReadOnlyList<ApprovalPolicyDto>> Handle(ListApprovalPolicies q, CancellationToken ct)
        => (await policies.ListAsync(null, ct)).Select(ToDto).OrderBy(p => p.DocumentType).ThenBy(p => p.Level).ThenBy(p => p.MinAmount).ToList();

    public async Task<ApprovalPolicyDto> Handle(SaveApprovalPolicy cmd, CancellationToken ct)
    {
        var policy = cmd.Id is { } id ? await policies.GetForUpdateAsync(id, ct) : new ApprovalPolicy();
        policy.DocumentType = cmd.Input.DocumentType; policy.MinAmount = cmd.Input.MinAmount; policy.IsActive = cmd.Input.IsActive;
        if (!string.IsNullOrWhiteSpace(cmd.Input.ApproverPermission)) policy.ApproverPermission = cmd.Input.ApproverPermission;
        if (cmd.Input.Level is { } level) policy.Level = level;
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

    private static ApprovalPolicyDto ToDto(ApprovalPolicy p) => new(p.Id, p.DocumentType, p.MinAmount, ApprovalRouting.EffectivePermission(p.ApproverPermission), p.IsActive, p.Level);
}
