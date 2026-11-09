using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosTenderRowDto(PosPaymentMethod Method, decimal Sales, decimal Refunds, decimal Net, int Count);

public record PosTopItemDto(string ItemCode, string Description, decimal Quantity, decimal Revenue);

/// <summary>Shift report: an X report while the shift is open, the Z report once it closes. Figures come from the documents of the shift.</summary>
public record PosShiftReportDto(
    PosSessionDto Session,
    decimal GrossSales,
    decimal Discounts,
    decimal NetSales,
    decimal Tax,
    decimal SalesTotal,
    decimal Returns,
    decimal Voids,
    decimal NetTakings,
    int SaleCount,
    int VoidCount,
    int ReturnCount,
    decimal ItemsSold,
    decimal AverageTicket,
    decimal CashSales,
    decimal CashRefunds,
    int HeldCount,
    List<PosTenderRowDto> Tenders,
    List<PosTopItemDto> TopItems);
