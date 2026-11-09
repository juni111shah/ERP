using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits an inspection that is not completed; its checklist is replaced.</summary>
public sealed record UpdateInspectionCommand(Guid Id, InspectionInput Input) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }
