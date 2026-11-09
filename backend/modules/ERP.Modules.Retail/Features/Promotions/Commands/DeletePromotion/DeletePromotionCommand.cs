using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Deletes a promotion.</summary>
public record DeletePromotionCommand(Guid Id) : ICommand;
