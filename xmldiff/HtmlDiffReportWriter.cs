using System.Net;
using System.Text;
using System.Xml.Linq;

internal static class HtmlDiffReportWriter
{
    public static string Write(XmlDiffReport report)
    {
        var sortRules = report.Rules.Select(rule => rule.Rule).ToArray();
        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>xmldiff report</title>
<style>
body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }
h1, h2, h3 { margin-bottom: 0.5rem; }
.summary { display: flex; gap: 12px; flex-wrap: wrap; margin: 1rem 0 1.5rem; }
.card { border: 1px solid #d1d5db; border-radius: 8px; padding: 12px 16px; min-width: 120px; background: #f9fafb; }
.rule { margin-top: 2rem; }
.note { color: #6b7280; }
.item { border: 1px solid #e5e7eb; border-radius: 8px; margin: 12px 0; overflow: hidden; }
.item.changed { border-left: 6px solid #dc2626; }
.item.left { border-left: 6px solid #2563eb; }
.item.right { border-left: 6px solid #059669; }
.item.duplicate { border-left: 6px solid #d97706; }
.header { background: #f3f4f6; padding: 10px 14px; font-weight: 600; }
.columns { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; padding: 12px; }
.column { min-width: 0; }
.details { padding: 12px; }
.details table { width: 100%; border-collapse: collapse; }
.details th, .details td { text-align: left; padding: 8px 10px; border-top: 1px solid #e5e7eb; vertical-align: top; }
.details th { background: #f9fafb; font-weight: 600; }
pre { white-space: pre-wrap; word-break: break-word; background: #111827; color: #f9fafb; padding: 12px; border-radius: 6px; }
.empty { color: #6b7280; font-style: italic; }
</style>
</head>
<body>
<h1>xmldiff report</h1>
""");
        builder.Append($"<p class=\"note\">Comments: {(report.CommentsIgnored ? "ignored" : "included")}</p>");
        builder.Append("<div class=\"summary\">");
        AppendCard(builder, "Matched", report.MatchedCount.ToString());
        AppendCard(builder, "Changed", report.ChangedCount.ToString());
        AppendCard(builder, "Only in left", report.OnlyInLeftCount.ToString());
        AppendCard(builder, "Only in right", report.OnlyInRightCount.ToString());
        AppendCard(builder, "Duplicate keys", report.DuplicateKeyCount.ToString());
        builder.Append("</div>");

        if (!report.HasDifferences)
        {
            builder.Append("<p>No differences found.</p>");
        }

        foreach (var rule in report.Rules)
        {
            builder.Append($"<section class=\"rule\"><h2>{Encode(SortRuleFormatter.Format(rule.Rule))}</h2>");
            builder.Append($"<p class=\"note\">Matched: {rule.MatchedCount} · Changed: {rule.ChangedCount} · Only in left: {rule.OnlyInLeftCount} · Only in right: {rule.OnlyInRightCount} · Duplicate keys: {rule.DuplicateKeys.Count}</p>");

            foreach (var conflict in rule.DuplicateKeys)
            {
                builder.Append($"<div class=\"item duplicate\"><div class=\"header\">Duplicate key: {Encode(conflict.Key)}</div><div class=\"columns\">");
                builder.Append(RenderColumn("Left", conflict.LeftElements));
                builder.Append(RenderColumn("Right", conflict.RightElements));
                builder.Append("</div></div>");
            }

            foreach (var difference in rule.Differences)
            {
                builder.Append($"<div class=\"item {GetCssClass(difference.Kind)}\"><div class=\"header\">{Encode(GetTitle(difference.Kind))}: {Encode(difference.Key)}</div>");

                if (ShouldRenderElementDetails(difference.Kind))
                {
                    builder.Append(RenderDetails(difference.LeftElement, difference.RightElement, sortRules, difference.TargetPathSegments));
                }

                builder.Append("</div>");
            }

            builder.Append("</section>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    private static bool ShouldRenderElementDetails(KeyedDifferenceKind kind)
    {
        return kind == KeyedDifferenceKind.Changed;
    }

    private static string RenderDetails(XElement? leftElement, XElement? rightElement, IReadOnlyList<SortRule> sortRules, IReadOnlyList<string> targetPathSegments)
    {
        if (leftElement is null || rightElement is null)
        {
            return string.Empty;
        }

        var details = ElementDifferenceDetailCollector.Collect(leftElement, rightElement, sortRules, targetPathSegments);

        if (details.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("<div class=\"details\"><table><thead><tr><th>Tag</th><th>Left</th><th>Right</th></tr></thead><tbody>");

        foreach (var detail in details)
        {
            builder.Append($"<tr><td>{Encode(detail.Path)}</td><td>{Encode(detail.LeftValue ?? string.Empty)}</td><td>{Encode(detail.RightValue ?? string.Empty)}</td></tr>");
        }

        builder.Append("</tbody></table></div>");
        return builder.ToString();
    }

    private static void AppendCard(StringBuilder builder, string title, string value)
    {
        builder.Append($"<div class=\"card\"><div>{Encode(title)}</div><strong>{Encode(value)}</strong></div>");
    }

    private static string RenderColumn(string title, XElement? element)
    {
        return $"<div class=\"column\"><h3>{Encode(title)}</h3>{RenderElement(element)}</div>";
    }

    private static string RenderColumn(string title, IReadOnlyList<XElement> elements)
    {
        if (elements.Count == 0)
        {
            return $"<div class=\"column\"><h3>{Encode(title)}</h3><div class=\"empty\">None</div></div>";
        }

        var builder = new StringBuilder();
        builder.Append($"<div class=\"column\"><h3>{Encode(title)}</h3>");

        foreach (var element in elements)
        {
            builder.Append(RenderElement(element));
        }

        builder.Append("</div>");
        return builder.ToString();
    }

    private static string RenderElement(XElement? element)
    {
        if (element is null)
        {
            return "<div class=\"empty\">None</div>";
        }

        return $"<pre>{Encode(element.ToString())}</pre>";
    }

    private static string GetCssClass(KeyedDifferenceKind kind)
    {
        return kind switch
        {
            KeyedDifferenceKind.Changed => "changed",
            KeyedDifferenceKind.OnlyInLeft => "left",
            KeyedDifferenceKind.OnlyInRight => "right",
            _ => string.Empty
        };
    }

    private static string GetTitle(KeyedDifferenceKind kind)
    {
        return kind switch
        {
            KeyedDifferenceKind.Changed => "Changed",
            KeyedDifferenceKind.OnlyInLeft => "Only in left",
            KeyedDifferenceKind.OnlyInRight => "Only in right",
            _ => kind.ToString()
        };
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
