using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Enters (or clears) counted quantities; an item not on the sheet is added as unexpected stock with system quantity 0.</summary>
public sealed record SaveCountedQuantitiesCommand(Guid Id, IReadOnlyList<CountedLineInput> Lines) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
