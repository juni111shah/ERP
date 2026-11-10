using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

internal sealed class ListApprovalPoliciesHandler(IReadRepository<ApprovalPolicy> policies) : IQueryHandler<ListApprovalPoliciesQuery, IReadOnlyList<ApprovalPolicyDto>>
{
    public async Task<IReadOnlyList<ApprovalPolicyDto>> Handle(ListApprovalPoliciesQuery query, CancellationToken ct)
        => (await policies.ListAsync(null, ct)).Select(ApprovalPolicyDto.From).OrderBy(p => p.DocumentType).ThenBy(p => p.Level).ThenBy(p => p.MinAmount).ToList();
}
