using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Labels a workspace as synthetic demonstration data. The label shows in the app and tells everyone its figures are not real.</summary>
public sealed record SetClientDemoCommand(Guid OrgId, bool IsDemo) : ICommand<bool>;
