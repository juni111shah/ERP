using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Deletes a cash register that has no open shift.</summary>
public record DeleteRegisterCommand(Guid Id) : ICommand;
