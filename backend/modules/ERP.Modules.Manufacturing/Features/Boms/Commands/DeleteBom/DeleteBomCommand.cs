using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Deletes a bill of materials that no work order uses.</summary>
public record DeleteBomCommand(Guid Id) : ICommand;
