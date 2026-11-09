using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Deletes a price list.</summary>
public record DeletePriceListCommand(Guid Id) : ICommand;
