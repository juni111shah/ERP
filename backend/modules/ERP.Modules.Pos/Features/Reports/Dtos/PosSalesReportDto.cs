namespace ERP.Modules.Pos.Features;

public record PosSalesDayDto(DateOnly Date, decimal Sales, decimal Returns, decimal Net, int Count);
public record PosSalesHourDto(int Hour, decimal Sales, int Count);
public record PosSalesGroupDto(string Name, decimal Sales, decimal Returns, decimal Net, int Count);

public record PosSalesReportDto(
    DateOnly From,
    DateOnly To,
    decimal GrossSales,
    decimal Discounts,
    decimal NetSales,
    decimal Tax,
    decimal SalesTotal,
    decimal Returns,
    decimal NetTakings,
    int SaleCount,
    int VoidCount,
    decimal VoidTotal,
    int ReturnCount,
    decimal AverageTicket,
    decimal CashOverShort,
    List<PosSalesDayDto> Days,
    List<PosSalesHourDto> Hours,
    List<PosTenderRowDto> Tenders,
    List<PosSalesGroupDto> Registers,
    List<PosSalesGroupDto> Cashiers,
    List<PosTopItemDto> TopItems);
