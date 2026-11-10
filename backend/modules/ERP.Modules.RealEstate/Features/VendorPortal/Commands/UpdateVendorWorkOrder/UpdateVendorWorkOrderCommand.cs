using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The vendor reports progress on their own order: start, hold or complete (with the actual cost and notes). Assignment, closing and cancelling stay with staff.</summary>
public sealed record UpdateVendorWorkOrderCommand(Guid Id, VendorWorkOrderUpdate Update) : ICommand<VendorWorkOrderDto>;
