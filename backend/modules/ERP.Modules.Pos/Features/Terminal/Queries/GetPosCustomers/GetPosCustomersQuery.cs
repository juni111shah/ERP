using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Customers the till can pick, matching part of the name, code, phone or tax number.</summary>
public record GetPosCustomersQuery(string? Search, int Limit = 20) : IQuery<List<PosCustomerDto>>;
