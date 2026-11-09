using System.Globalization;
using System.Net;
using System.Text;

namespace ERP.Application.Printing;

/// <summary>Plain data a document is rendered from; HTML and text bodies are built from it so both always agree.</summary>
internal sealed record EmailDoc(string Title, string Company, string Currency, IReadOnlyList<(string Label, string Value)> Facts,
    IReadOnlyList<(string Description, string Qty, string Price, string Amount)> Lines, IReadOnlyList<(string Label, string Value)> Totals, string? Message);

internal static class DocumentEmailRenderer
{
    public static string Money(decimal v, string currency) => $"{v.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    public static string Text(EmailDoc d)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(d.Message)) sb.AppendLine(d.Message).AppendLine();
        sb.AppendLine(d.Title).AppendLine(d.Company).AppendLine();
        foreach (var (l, v) in d.Facts) sb.AppendLine($"{l}: {v}");
        if (d.Lines.Count > 0) { sb.AppendLine(); foreach (var l in d.Lines) sb.AppendLine($"- {l.Description}  {l.Qty} x {l.Price} = {l.Amount}"); }
        if (d.Totals.Count > 0) { sb.AppendLine(); foreach (var (l, v) in d.Totals) sb.AppendLine($"{l}: {v}"); }
        return sb.ToString().TrimEnd();
    }

    public static string Html(EmailDoc d)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;max-width:640px\">");
        if (!string.IsNullOrWhiteSpace(d.Message)) sb.Append($"<p>{E(d.Message).Replace("\n", "<br>")}</p>");
        sb.Append($"<h2 style=\"margin:16px 0 4px\">{E(d.Title)}</h2><div style=\"color:#6b7280\">{E(d.Company)}</div>");
        sb.Append("<table style=\"margin-top:12px;border-collapse:collapse\">");
        foreach (var (l, v) in d.Facts) sb.Append($"<tr><td style=\"padding:2px 16px 2px 0;color:#6b7280\">{E(l)}</td><td>{E(v)}</td></tr>");
        sb.Append("</table>");
        if (d.Lines.Count > 0)
        {
            sb.Append("<table style=\"margin-top:16px;border-collapse:collapse;width:100%\"><tr style=\"background:#f3f4f6\"><th align=\"left\" style=\"padding:6px\">Description</th><th align=\"right\">Qty</th><th align=\"right\">Price</th><th align=\"right\" style=\"padding-right:6px\">Amount</th></tr>");
            foreach (var l in d.Lines) sb.Append($"<tr style=\"border-bottom:1px solid #e5e7eb\"><td style=\"padding:6px\">{E(l.Description)}</td><td align=\"right\">{E(l.Qty)}</td><td align=\"right\">{E(l.Price)}</td><td align=\"right\" style=\"padding-right:6px\">{E(l.Amount)}</td></tr>");
            sb.Append("</table>");
        }
        if (d.Totals.Count > 0)
        {
            sb.Append("<table style=\"margin-top:12px;margin-left:auto;border-collapse:collapse\">");
            foreach (var (l, v) in d.Totals) sb.Append($"<tr><td style=\"padding:2px 16px 2px 0;color:#6b7280\">{E(l)}</td><td align=\"right\"><b>{E(v)}</b></td></tr>");
            sb.Append("</table>");
        }
        return sb.Append("</div>").ToString();
    }
}
