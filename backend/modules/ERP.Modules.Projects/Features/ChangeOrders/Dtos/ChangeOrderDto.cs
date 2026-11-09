using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record ChangeOrderInput(Guid ProjectId, string Title, string? Description, decimal AmountChange, decimal HoursChange = 0m)
{
    /// <summary>Copies the form onto the change order; shared by create and update.</summary>
    public void ApplyTo(ProjectChangeOrder c) { c.Title = Title.Trim(); c.Description = Description?.Trim(); c.AmountChange = AmountChange; c.HoursChange = HoursChange; }
}

public sealed record ChangeOrderDto(Guid Id, Guid ProjectId, string Number, string Title, string? Description, decimal AmountChange, decimal HoursChange, ChangeOrderStatus Status, DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt)
{
    public static ChangeOrderDto From(ProjectChangeOrder c) => new(c.Id, c.ProjectId, c.Number, c.Title, c.Description, c.AmountChange, c.HoursChange, c.Status, c.DecidedAt, c.DecisionNote, c.CreatedAt);
}
