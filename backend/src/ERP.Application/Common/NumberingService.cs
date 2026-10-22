using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common;

/// <summary>Allocates document numbers (INV-2026-000001) from the workspace's sequences. Saved with the caller's unit of work.</summary>
public sealed class NumberingService(IRepository<NumberSequence> sequences, IClock clock) : INumberingService
{
    // Sequences created during this request but not saved yet; a second number in the same unit of work must continue them.
    private readonly Dictionary<(string Type, Guid? Entity, int Year), NumberSequence> created = new();

    public async Task<string> NextAsync(string documentType, Guid? legalEntityId = null, DateOnly? date = null, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(clock.UtcNow);
        var template = await sequences.Query(new Specification<NumberSequence>(s => s.DocumentType == documentType && (s.LegalEntityId == legalEntityId || s.LegalEntityId == null)))
                           .OrderByDescending(s => s.LegalEntityId != null).ThenByDescending(s => s.Year).FirstOrDefaultAsync(ct);
        if (template is null)
        {
            // Workspaces created before a document type existed get its default sequence on first use.
            var known = ERP.Application.Templates.CoreTemplates.Sequences.FirstOrDefault(s => s.DocumentType == documentType)
                        ?? throw new ConflictException($"No number sequence is configured for '{documentType}'.");
            template = new NumberSequence { DocumentType = documentType, LegalEntityId = null, Prefix = known.Prefix, Pattern = known.Pattern, ResetYearly = known.ResetYearly, Year = 0, NextValue = 1 };
        }
        var year = template.ResetYearly ? d.Year : 0;
        var key = (documentType, template.LegalEntityId, year);
        var seq = created.GetValueOrDefault(key)
                  ?? await sequences.Tracked(new Specification<NumberSequence>(s => s.DocumentType == documentType && s.LegalEntityId == template.LegalEntityId && s.Year == year)).FirstOrDefaultAsync(ct);
        if (seq is null)
        {
            seq = new NumberSequence { DocumentType = documentType, LegalEntityId = template.LegalEntityId, Prefix = template.Prefix, Pattern = template.Pattern, ResetYearly = template.ResetYearly, Year = year, NextValue = 1 };
            sequences.Add(seq);
            created[key] = seq;
        }
        var value = seq.NextValue++;
        // Saved with the caller's SaveChanges (same transaction); rowversion makes concurrent allocation fail instead of duplicating.
        return NumberFormatter.Format(seq.Pattern, seq.Prefix, d, value);
    }
}
