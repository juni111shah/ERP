using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common;

/// <summary>Allocates document numbers (INV-2026-000001) from the workspace's sequences. Saved with the caller's unit of work.</summary>
public sealed class NumberingService(IRepository<NumberSequence> sequences, IClock clock) : INumberingService
{
    public async Task<string> NextAsync(string documentType, Guid? legalEntityId = null, DateOnly? date = null, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(clock.UtcNow);
        var template = await sequences.Query(new Specification<NumberSequence>(s => s.DocumentType == documentType && (s.LegalEntityId == legalEntityId || s.LegalEntityId == null)))
                           .OrderByDescending(s => s.LegalEntityId != null).ThenByDescending(s => s.Year).FirstOrDefaultAsync(ct)
                       ?? throw new ConflictException($"No number sequence is configured for '{documentType}'.");
        var year = template.ResetYearly ? d.Year : 0;
        var seq = await sequences.Tracked(new Specification<NumberSequence>(s => s.DocumentType == documentType && s.LegalEntityId == template.LegalEntityId && s.Year == year)).FirstOrDefaultAsync(ct);
        if (seq is null)
        {
            seq = new NumberSequence { DocumentType = documentType, LegalEntityId = template.LegalEntityId, Prefix = template.Prefix, Pattern = template.Pattern, ResetYearly = template.ResetYearly, Year = year, NextValue = 1 };
            sequences.Add(seq);
        }
        var value = seq.NextValue++;
        // Saved with the caller's SaveChanges (same transaction); rowversion makes concurrent allocation fail instead of duplicating.
        return NumberFormatter.Format(seq.Pattern, seq.Prefix, d, value);
    }
}
