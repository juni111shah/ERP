namespace ERP.Modules.Manufacturing.Features;

public record WorkOrderOperationInput(
    int SequenceOrder,
    string OperationName,
    string WorkCenterName,
    decimal PlannedHours,
    decimal HourlyRate);

public record MaterialLineInput(Guid ItemId, decimal Quantity, string? Notes = null);

public record OperationHoursInput(Guid OperationId, decimal ActualHours);
