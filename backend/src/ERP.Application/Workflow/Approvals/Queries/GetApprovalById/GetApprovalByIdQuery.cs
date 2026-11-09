using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>One request with its decision history, for the requester, whoever can decide one of its levels and auditors.</summary>
public sealed record GetApprovalByIdQuery(Guid Id) : IQuery<ApprovalDto>;
