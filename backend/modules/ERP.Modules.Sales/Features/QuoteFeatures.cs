using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Sales.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

public sealed record QuoteLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record QuoteInput(
    Guid LegalEntityId,
    string? Number,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    string? Notes,
    string? Terms,
    List<QuoteLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record QuoteLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal);

public sealed record QuoteSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    SalesQuoteStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? ConvertedSalesOrderId,
    DateTime CreatedAt);

public sealed record QuoteDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    SalesQuoteStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? Notes,
    string? Terms,
    Guid? ConvertedSalesOrderId,
    IReadOnlyList<QuoteLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt);

public sealed record GetQuote(Guid Id) : IQuery<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesRead; }
public sealed record CreateQuote(QuoteInput Input) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
public sealed record UpdateQuote(Guid Id, QuoteInput Input) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
public sealed record DeleteQuote(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
public sealed record ChangeQuoteStatus(Guid Id, SalesQuoteStatus Status) : ICommand<QuoteDto>, IRequirePermission { public string RequiredPermission => P.QuotesManage; }
public sealed record ConvertQuoteToOrder(Guid Id) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public sealed class QuoteInputValidator : AbstractValidator<QuoteInput>
{
    public QuoteInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Quote must contain at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0, 100);
        });
    }
}

public sealed class CreateQuoteValidator : AbstractValidator<CreateQuote>
{
    public CreateQuoteValidator() => RuleFor(x => x.Input).SetValidator(new QuoteInputValidator());
}

public sealed class UpdateQuoteValidator : AbstractValidator<UpdateQuote>
{
    public UpdateQuoteValidator() => RuleFor(x => x.Input).SetValidator(new QuoteInputValidator());
}

internal sealed class QuoteHandlers(
    IRepository<SalesQuote> quotes,
    IRepository<SalesOrder> orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) :
    IRequestHandler<GetQuote, QuoteDto>,
    IRequestHandler<CreateQuote, QuoteDto>,
    IRequestHandler<UpdateQuote, QuoteDto>,
    IRequestHandler<DeleteQuote, Unit>,
    IRequestHandler<ChangeQuoteStatus, QuoteDto>,
    IRequestHandler<ConvertQuoteToOrder, OrderDto>
{
    public async Task<QuoteDto> Handle(GetQuote q, CancellationToken ct)
    {
        var quote = await quotes.Tracked(new Specification<SalesQuote>(x => x.Id == q.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Quote '{q.Id}' not found.");
        return await ToDtoAsync(quote, ct);
    }

    public async Task<QuoteDto> Handle(CreateQuote cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var quote = new SalesQuote
        {
            LegalEntityId = entity.Id,
            CustomerPartyId = i.CustomerPartyId,
            CustomerName = i.CustomerName.Trim(),
            CustomerEmail = i.CustomerEmail?.Trim(),
            CustomerPhone = i.CustomerPhone?.Trim(),
            QuoteDate = i.QuoteDate,
            ValidUntil = i.ValidUntil,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            Notes = i.Notes,
            Terms = i.Terms,
            Status = SalesQuoteStatus.Draft,
        };

        quote.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.Quote, entity.Id, i.QuoteDate, ct)
            : i.Number.Trim();

        if (await quotes.AnyAsync(x => x.Number == quote.Number, ct))
            throw new ConflictException($"Quote number '{quote.Number}' already exists.");

        PopulateLinesAndTotals(quote, i.Lines);
        await fields.ApplyAsync(Docs.Quote, quote, i.CustomFields, isCreate: true, ct);

        quotes.Add(quote);
        await quotes.SaveChangesAsync(ct);
        return await ToDtoAsync(quote, ct);
    }

    public async Task<QuoteDto> Handle(UpdateQuote cmd, CancellationToken ct)
    {
        var quote = await quotes.Tracked(new Specification<SalesQuote>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        if (quote.Status == SalesQuoteStatus.Converted)
            throw new ConflictException("Cannot edit a converted quote.");

        var i = cmd.Input;
        quote.CustomerPartyId = i.CustomerPartyId;
        quote.CustomerName = i.CustomerName.Trim();
        quote.CustomerEmail = i.CustomerEmail?.Trim();
        quote.CustomerPhone = i.CustomerPhone?.Trim();
        quote.QuoteDate = i.QuoteDate;
        quote.ValidUntil = i.ValidUntil;
        quote.Currency = i.Currency.Trim().ToUpperInvariant();
        quote.Notes = i.Notes;
        quote.Terms = i.Terms;

        PopulateLinesAndTotals(quote, i.Lines);
        await fields.ApplyAsync(Docs.Quote, quote, i.CustomFields, isCreate: false, ct);

        await quotes.SaveChangesAsync(ct);
        return await ToDtoAsync(quote, ct);
    }

    public async Task<Unit> Handle(DeleteQuote cmd, CancellationToken ct)
    {
        var quote = await quotes.GetForUpdateAsync(cmd.Id, ct);
        if (quote.Status == SalesQuoteStatus.Converted)
            throw new ConflictException("Cannot delete a converted quote.");

        quotes.Remove(quote);
        await quotes.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<QuoteDto> Handle(ChangeQuoteStatus cmd, CancellationToken ct)
    {
        var quote = await quotes.Tracked(new Specification<SalesQuote>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        SalesTransitions.Quote.Ensure(quote.Status, cmd.Status, isSystem: false);

        quote.Status = cmd.Status;
        await quotes.SaveChangesAsync(ct);
        return await ToDtoAsync(quote, ct);
    }

    public async Task<OrderDto> Handle(ConvertQuoteToOrder cmd, CancellationToken ct)
    {
        var quote = await quotes.Tracked(new Specification<SalesQuote>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        if (quote.Status == SalesQuoteStatus.Converted && quote.ConvertedSalesOrderId.HasValue)
        {
            var existingOrder = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == quote.ConvertedSalesOrderId.Value))
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(ct);
            if (existingOrder != null) return await OrderHandlers.ToDtoAsync(existingOrder, fields, ct);
        }

        SalesTransitions.Quote.Ensure(quote.Status, SalesQuoteStatus.Converted, isSystem: true);

        var order = new SalesOrder
        {
            LegalEntityId = quote.LegalEntityId,
            QuoteId = quote.Id,
            QuoteNumber = quote.Number,
            CustomerPartyId = quote.CustomerPartyId,
            CustomerName = quote.CustomerName,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = quote.Currency,
            Status = SalesOrderStatus.Confirmed,
            PaymentTerms = quote.Terms,
            Notes = quote.Notes,
            Number = await numbers.NextAsync(Docs.SalesOrder, quote.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), ct),
        };

        foreach (var l in quote.Lines)
        {
            order.Lines.Add(new SalesOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = l.LineTotal,
            });
        }

        order.SubTotal = quote.SubTotal;
        order.TaxTotal = quote.TaxTotal;
        order.DiscountTotal = quote.DiscountTotal;
        order.TotalAmount = quote.TotalAmount;

        orders.Add(order);
        quote.Status = SalesQuoteStatus.Converted;
        quote.ConvertedSalesOrderId = order.Id;

        await quotes.SaveChangesAsync(ct);
        await orders.SaveChangesAsync(ct);

        return await OrderHandlers.ToDtoAsync(order, fields, ct);
    }

    private static void PopulateLinesAndTotals(SalesQuote quote, List<QuoteLineInput> lines)
    {
        quote.Lines.Clear();
        decimal subtotal = 0;
        decimal taxTotal = 0;
        decimal discountTotal = 0;

        foreach (var l in lines)
        {
            var baseAmount = l.Quantity * l.UnitPrice;
            var disc = baseAmount * (l.DiscountPercent / 100m);
            var afterDisc = baseAmount - disc;
            var tax = afterDisc * (l.TaxRate / 100m);
            var lineTotal = afterDisc + tax;

            subtotal += afterDisc;
            discountTotal += disc;
            taxTotal += tax;

            quote.Lines.Add(new SalesQuoteLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim(),
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = Math.Round(lineTotal, 2)
            });
        }

        quote.SubTotal = Math.Round(subtotal, 2);
        quote.DiscountTotal = Math.Round(discountTotal, 2);
        quote.TaxTotal = Math.Round(taxTotal, 2);
        quote.TotalAmount = Math.Round(subtotal + taxTotal, 2);
    }

    private async Task<QuoteDto> ToDtoAsync(SalesQuote q, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.Quote, q, ct);
        return new QuoteDto(
            q.Id,
            q.Number,
            q.LegalEntityId,
            q.CustomerPartyId,
            q.CustomerName,
            q.CustomerEmail,
            q.CustomerPhone,
            q.QuoteDate,
            q.ValidUntil,
            q.Currency,
            q.Status,
            q.SubTotal,
            q.TaxTotal,
            q.DiscountTotal,
            q.TotalAmount,
            q.Notes,
            q.Terms,
            q.ConvertedSalesOrderId,
            q.Lines.Select(l => new QuoteLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitOfMeasure,
                l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal)).ToList(),
            cf,
            q.CreatedAt);
    }
}
