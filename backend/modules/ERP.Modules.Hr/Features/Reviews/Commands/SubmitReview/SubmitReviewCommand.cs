using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Locks the review for the employee to read; needs an overall rating.</summary>
public sealed record SubmitReviewCommand(Guid Id) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
