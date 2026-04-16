using System.Xml.Linq;

internal static class ElementDifferenceDetailCollector
{
    private const string MissingValue = "<missing>";
    private const string PresentValue = "<present>";

    public static IReadOnlyList<ElementDifferenceDetail> Collect(
        XElement leftElement,
        XElement rightElement,
        IReadOnlyList<SortRule> sortRules,
        IReadOnlyList<string> currentPathSegments)
    {
        var differences = new List<ElementDifferenceDetail>();
        Collect(leftElement, rightElement, string.Empty, currentPathSegments, sortRules, differences);
        return differences;
    }

    private static void Collect(
        XElement leftElement,
        XElement rightElement,
        string path,
        IReadOnlyList<string> currentPathSegments,
        IReadOnlyList<SortRule> sortRules,
        List<ElementDifferenceDetail> differences)
    {
        CollectAttributeDifferences(leftElement, rightElement, path, differences);

        var leftChildren = leftElement.Elements().ToList();
        var rightChildren = rightElement.Elements().ToList();

        if (leftChildren.Count == 0 && rightChildren.Count == 0)
        {
            var leftValue = leftElement.Value;
            var rightValue = rightElement.Value;

            if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
            {
                differences.Add(new ElementDifferenceDetail(FormatElementPath(path), leftValue, rightValue));
            }

            return;
        }

        var childNames = leftChildren.Select(child => child.Name.LocalName)
            .Union(rightChildren.Select(child => child.Name.LocalName), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (var childName in childNames)
        {
            var leftMatches = leftChildren.Where(child => child.Name.LocalName == childName).ToArray();
            var rightMatches = rightChildren.Where(child => child.Name.LocalName == childName).ToArray();
            var maxCount = Math.Max(leftMatches.Length, rightMatches.Length);
            var childPathSegments = new string[currentPathSegments.Count + 1];

            for (var pathIndex = 0; pathIndex < currentPathSegments.Count; pathIndex++)
            {
                childPathSegments[pathIndex] = currentPathSegments[pathIndex];
            }

            childPathSegments[^1] = childName;
            var keyedRule = FindApplicableRule(sortRules, childPathSegments);

            for (var index = 0; index < maxCount; index++)
            {
                var hasLeft = index < leftMatches.Length;
                var hasRight = index < rightMatches.Length;
                var matchedElement = hasLeft ? leftMatches[index] : hasRight ? rightMatches[index] : null;
                var childSegment = FormatChildSegment(childName, keyedRule, matchedElement, maxCount > 1 ? index + 1 : null);
                var childPath = AppendPath(path, childSegment);

                if (hasLeft && hasRight)
                {
                    Collect(leftMatches[index], rightMatches[index], childPath, childPathSegments, sortRules, differences);
                    continue;
                }

                differences.Add(new ElementDifferenceDetail(
                    childPath,
                    hasLeft ? SummarizeElement(leftMatches[index]) : MissingValue,
                    hasRight ? SummarizeElement(rightMatches[index]) : MissingValue));
            }
        }
    }

    private static SortRule? FindApplicableRule(IReadOnlyList<SortRule> sortRules, IReadOnlyList<string> pathSegments)
    {
        return sortRules
            .Where(rule => XmlPathPatternMatcher.IsPathMatch(pathSegments, rule.PathSegments))
            .OrderByDescending(rule => rule.PathSegments.Count)
            .ThenBy(rule => rule.PathSegments.Count(segment => segment.Contains('*', StringComparison.Ordinal)))
            .FirstOrDefault();
    }

    private static string FormatChildSegment(string childName, SortRule? keyedRule, XElement? element, int? index)
    {
        if (keyedRule is not null && element is not null)
        {
            return BuildCompositeKey(element, keyedRule.Keys);
        }

        return index is null ? childName : $"{childName}[{index.Value}]";
    }

    private static void CollectAttributeDifferences(XElement leftElement, XElement rightElement, string path, List<ElementDifferenceDetail> differences)
    {
        var attributeNames = leftElement.Attributes().Select(attribute => attribute.Name.LocalName)
            .Union(rightElement.Attributes().Select(attribute => attribute.Name.LocalName), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var attributeName in attributeNames)
        {
            var leftValue = leftElement.Attribute(attributeName)?.Value;
            var rightValue = rightElement.Attribute(attributeName)?.Value;

            if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
            {
                differences.Add(new ElementDifferenceDetail(
                    AppendAttributePath(path, attributeName),
                    leftValue ?? MissingValue,
                    rightValue ?? MissingValue));
            }
        }
    }

    private static string SummarizeElement(XElement element)
    {
        var hasAttributes = element.HasAttributes;
        var hasChildElements = element.Elements().Any();

        if (!hasAttributes && !hasChildElements)
        {
            return element.Value;
        }

        return PresentValue;
    }

    private static string BuildCompositeKey(XElement element, IReadOnlyList<SortKey> keys)
    {
        var values = XmlSorter.GetSortValues(element, keys);
        var parts = new string[keys.Count];

        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];
            var value = values[keyIndex];
            var displayName = key.Kind == SortKeyKind.Attribute ? $"@{key.Name}" : key.Name;
            parts[keyIndex] = $"{displayName}={value.Text}";
        }

        return string.Join(", ", parts);
    }

    private static string AppendPath(string path, string segment)
    {
        return string.IsNullOrEmpty(path) ? segment : $"{path}/{segment}";
    }

    private static string AppendAttributePath(string path, string attributeName)
    {
        return string.IsNullOrEmpty(path) ? $"@{attributeName}" : $"{path}/@{attributeName}";
    }

    private static string FormatElementPath(string path)
    {
        return string.IsNullOrEmpty(path) ? "." : path;
    }
}
