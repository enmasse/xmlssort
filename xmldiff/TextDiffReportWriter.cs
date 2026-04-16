using System.Text;
using System.Xml.Linq;

internal static class TextDiffReportWriter
{
    public static string Write(XmlDiffReport report, bool formatXml)
    {
        var sortRules = report.Rules.Select(rule => rule.Rule).ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("xmldiff report");
        builder.AppendLine($"Matched: {report.MatchedCount}");
        builder.AppendLine($"Changed: {report.ChangedCount}");
        builder.AppendLine($"Only in left: {report.OnlyInLeftCount}");
        builder.AppendLine($"Only in right: {report.OnlyInRightCount}");
        builder.AppendLine($"Duplicate keys: {report.DuplicateKeyCount}");
        builder.AppendLine(report.CommentsIgnored ? "Comments: ignored" : "Comments: included");

        if (!report.HasDifferences)
        {
            builder.AppendLine();
            builder.AppendLine("No differences found.");
            return builder.ToString();
        }

        foreach (var rule in report.Rules)
        {
            builder.AppendLine();
            builder.AppendLine($"Rule: {SortRuleFormatter.Format(rule.Rule)}");
            builder.AppendLine($"  Matched: {rule.MatchedCount}");
            builder.AppendLine($"  Changed: {rule.ChangedCount}");
            builder.AppendLine($"  Only in left: {rule.OnlyInLeftCount}");
            builder.AppendLine($"  Only in right: {rule.OnlyInRightCount}");
            builder.AppendLine($"  Duplicate keys: {rule.DuplicateKeys.Count}");

            foreach (var conflict in rule.DuplicateKeys)
            {
                builder.AppendLine($"  Duplicate key: {conflict.Key}");
                AppendElements(builder, "    Left", conflict.LeftElements, formatXml);
                AppendElements(builder, "    Right", conflict.RightElements, formatXml);
            }

            foreach (var difference in rule.Differences)
            {
                builder.AppendLine($"  {FormatKind(difference.Kind)}: {difference.Key}");

                if (!ShouldRenderElementDetails(difference.Kind))
                {
                    continue;
                }

                if (difference.LeftElement is not null && difference.RightElement is not null)
                {
                    AppendDifferenceDetails(builder, ElementDifferenceDetailCollector.Collect(difference.LeftElement, difference.RightElement, sortRules, difference.TargetPathSegments));
                }
            }
        }

        return builder.ToString();
    }

    private static bool ShouldRenderElementDetails(KeyedDifferenceKind kind)
    {
        return kind == KeyedDifferenceKind.Changed;
    }

    private static void AppendDifferenceDetails(StringBuilder builder, IReadOnlyList<ElementDifferenceDetail> details)
    {
        foreach (var detail in details)
        {
            builder.AppendLine($"    {detail.Path}: left='{detail.LeftValue ?? string.Empty}', right='{detail.RightValue ?? string.Empty}'");
        }
    }

    private static void AppendElements(StringBuilder builder, string label, IReadOnlyList<XElement> elements, bool formatXml)
    {
        if (elements.Count == 0)
        {
            return;
        }

        foreach (var element in elements)
        {
            AppendElement(builder, label, element, formatXml);
        }
    }

    private static void AppendElement(StringBuilder builder, string label, XElement element, bool formatXml)
    {
        var text = formatXml ? element.ToString() : element.ToString(SaveOptions.DisableFormatting);
        builder.AppendLine($"{label}: {text}");
    }

    private static string FormatKind(KeyedDifferenceKind kind)
    {
        return kind switch
        {
            KeyedDifferenceKind.Changed => "Changed",
            KeyedDifferenceKind.OnlyInLeft => "Only in left",
            KeyedDifferenceKind.OnlyInRight => "Only in right",
            _ => kind.ToString()
        };
    }
}
