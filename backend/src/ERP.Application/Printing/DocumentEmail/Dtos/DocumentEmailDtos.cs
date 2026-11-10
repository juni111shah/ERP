namespace ERP.Application.Printing;

public enum EmailDocKind { Invoice, Receipt, Bill, Payment, Statement }

/// <summary>Recipient (defaults to the party's email) and an optional short message shown above the document.</summary>
public sealed record EmailDocumentInput(string? To, string? Message);
public sealed record EmailSentDto(string To, string Subject);
