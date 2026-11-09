using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a vacancy without candidates (others are closed instead).</summary>
public sealed record DeleteJobOpeningCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
