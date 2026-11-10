using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modularity.Features;

internal sealed class UpdateNumberSequenceHandler(INumberSequenceRepository sequences, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<UpdateNumberSequenceCommand, NumberSequenceView>
{
    public async Task<NumberSequenceView> Handle(UpdateNumberSequenceCommand command, CancellationToken ct)
    {
        var rows = await sequences.ListForDocumentTypeForUpdateAsync(command.DocumentType, ct);
        if (rows.Count == 0) throw new NotFoundException("That document type has no numbering yet.");
        foreach (var row in rows)
        {
            row.Prefix = command.Input.Prefix.Trim();
            row.Pattern = command.Input.Pattern.Trim();
            row.ResetYearly = command.Input.ResetYearly;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return NumberSequenceView.From(rows.OrderByDescending(s => s.Year).First(), DateOnly.FromDateTime(clock.UtcNow));
    }
}
