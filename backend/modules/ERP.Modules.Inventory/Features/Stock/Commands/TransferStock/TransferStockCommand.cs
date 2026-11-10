using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Moves stock between two warehouses of the same legal entity at the source's average cost (no posting).</summary>
public sealed record TransferStockCommand(TransferStockInput Input) : ICommand<IReadOnlyList<MovementDto>>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
