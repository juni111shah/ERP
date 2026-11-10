using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Edits a draft review and replaces its competencies; the employee and cycle stay.</summary>
public sealed record UpdateReviewCommand(Guid Id, ReviewInput Input) : ICommand<ReviewDto>, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
