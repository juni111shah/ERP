using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a draft review.</summary>
public sealed record DeleteReviewCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReviewsManage; }
