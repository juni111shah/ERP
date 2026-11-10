using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Reverses a completed work order: takes the finished goods back, returns the material and reverses its journals.</summary>
public record ReverseWorkOrderCommand(Guid Id, string Reason) : ICommand<WorkOrderDto>;
