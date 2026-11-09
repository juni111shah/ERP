using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Records that the employee has seen and acknowledged the submitted review.</summary>
public sealed record AcknowledgeReviewCommand(Guid Id) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
