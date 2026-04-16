using System.Globalization;
using System.Xml.Linq;

internal static class XmlSorter
{
    public static void Apply(XDocument document, IReadOnlyList<SortRule> rules, bool sortByTagName = false)
    {
        if (document.Root is null)
        {
            throw new ArgumentException("The XML document does not have a root element.");
        }

        var compiledRules = CompileRules(rules);

        if (sortByTagName)
        {
            SortElementsByTagName(document.Root);
            PromoteSortKeys(document.Root, compiledRules, []);
        }

        Apply(document.Root, compiledRules, []);
    }

    internal static void SortElementsByTagName(XElement element)
    {
        var childElements = element.Elements().ToList();

        if (childElements.Count >= 2)
        {
            var sorted = childElements.OrderBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase).ToArray();

            if (!childElements.SequenceEqual(sorted))
            {
                var newElements = new XElement[childElements.Count];

                for (var i = 0; i < childElements.Count; i++)
                {
                    var newElement = new XElement(sorted[i]);
                    childElements[i].ReplaceWith(newElement);
                    newElements[i] = newElement;
                }

                foreach (var child in newElements)
                {
                    SortElementsByTagName(child);
                }

                return;
            }
        }

        foreach (var child in childElements)
        {
            SortElementsByTagName(child);
        }
    }

    internal static CompiledSortRule[] CompileRules(IReadOnlyList<SortRule> rules)
    {
        var compiledRules = new CompiledSortRule[rules.Count];

        for (var index = 0; index < rules.Count; index++)
        {
            compiledRules[index] = new CompiledSortRule(rules[index]);
        }

        return compiledRules;
    }

    internal static void PromoteSortKeys(XElement root, IReadOnlyList<CompiledSortRule> rules, IReadOnlyList<string> pathPrefix)
    {
        foreach (var rule in rules)
        {
            PromoteRuleKeys(root, rule, pathPrefix);
        }
    }

    internal static void Apply(XElement root, IReadOnlyList<CompiledSortRule> rules, IReadOnlyList<string> pathPrefix)
    {
        foreach (var rule in rules)
        {
            ApplyRule(root, rule, pathPrefix);
        }
    }

    private static void ApplyRule(XElement root, CompiledSortRule rule, IReadOnlyList<string> pathPrefix)
    {
        var topLevelParents = new List<XElement>();
        var currentPath = new List<string>(pathPrefix.Count + 8);
        currentPath.AddRange(pathPrefix);

        CollectTopLevelParents(root, rule.ParentPathMatcher, false, currentPath, topLevelParents);

        foreach (var parent in topLevelParents)
        {
            SortChildrenRecursively(parent, rule.ParentMatcher, rule.TargetMatcher, rule.Keys);
        }
    }

    private static void PromoteRuleKeys(XElement root, CompiledSortRule rule, IReadOnlyList<string> pathPrefix)
    {
        var topLevelParents = new List<XElement>();
        var currentPath = new List<string>(pathPrefix.Count + 8);
        currentPath.AddRange(pathPrefix);

        CollectTopLevelParents(root, rule.ParentPathMatcher, false, currentPath, topLevelParents);

        foreach (var parent in topLevelParents)
        {
            PromoteTargetKeysRecursively(parent, rule.ParentMatcher, rule.TargetMatcher, rule.Keys);
        }
    }

    private static void CollectTopLevelParents(
        XElement current,
        XmlPathPatternMatcher.PathMatcher parentPathMatcher,
        bool isWithinMatchedParent,
        List<string> currentPath,
        List<XElement> topLevelParents)
    {
        currentPath.Add(current.Name.LocalName);

        var isMatchedParent = parentPathMatcher.IsMatch(currentPath);

        if (isMatchedParent && !isWithinMatchedParent)
        {
            topLevelParents.Add(current);
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        foreach (var child in current.Elements())
        {
            CollectTopLevelParents(child, parentPathMatcher, isWithinMatchedParent, currentPath, topLevelParents);
        }

        currentPath.RemoveAt(currentPath.Count - 1);
    }

    private static void SortChildrenRecursively(
        XElement current,
        XmlPathPatternMatcher.SegmentMatcher parentMatcher,
        XmlPathPatternMatcher.SegmentMatcher targetMatcher,
        IReadOnlyList<SortKey> keys)
    {
        if (parentMatcher.IsMatch(current.Name.LocalName))
        {
            SortChildren(current, targetMatcher, keys);
        }

        foreach (var child in current.Elements().ToList())
        {
            SortChildrenRecursively(child, parentMatcher, targetMatcher, keys);
        }
    }

    private static void PromoteTargetKeysRecursively(
        XElement current,
        XmlPathPatternMatcher.SegmentMatcher parentMatcher,
        XmlPathPatternMatcher.SegmentMatcher targetMatcher,
        IReadOnlyList<SortKey> keys)
    {
        if (parentMatcher.IsMatch(current.Name.LocalName))
        {
            PromoteTargetKeys(current, targetMatcher, keys);
        }

        foreach (var child in current.Elements().ToList())
        {
            PromoteTargetKeysRecursively(child, parentMatcher, targetMatcher, keys);
        }
    }

    private static void SortChildren(XElement parent, XmlPathPatternMatcher.SegmentMatcher targetMatcher, IReadOnlyList<SortKey> keys)
    {
        var targetMatchCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        var targetElements = parent.Elements()
            .Where(element => IsTargetElementMatch(element, targetMatcher, targetMatchCache))
            .ToList();

        if (targetElements.Count < 2)
        {
            return;
        }

        var sortedTargets = SortElements(targetElements, keys).ToArray();

        if (targetElements.SequenceEqual(sortedTargets))
        {
            return;
        }

        for (var targetIndex = 0; targetIndex < targetElements.Count; targetIndex++)
        {
            targetElements[targetIndex].ReplaceWith(new XElement(sortedTargets[targetIndex]));
        }
    }

    private static void PromoteTargetKeys(XElement parent, XmlPathPatternMatcher.SegmentMatcher targetMatcher, IReadOnlyList<SortKey> keys)
    {
        var targetMatchCache = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var targetElement in parent.Elements().Where(element => IsTargetElementMatch(element, targetMatcher, targetMatchCache)).ToList())
        {
            PromoteElementKeys(targetElement, keys);
        }
    }

    private static void PromoteElementKeys(XElement element, IReadOnlyList<SortKey> keys)
    {
        var childElements = element.Elements().ToList();

        if (childElements.Count < 2)
        {
            return;
        }

        var keyNames = GetPromotedKeyNames(keys);

        if (keyNames.Length == 0)
        {
            return;
        }

        var promotedElements = new List<XElement>(childElements.Count);

        foreach (var keyName in keyNames)
        {
            promotedElements.AddRange(childElements.Where(child => string.Equals(child.Name.LocalName, keyName, StringComparison.Ordinal)));
        }

        promotedElements.AddRange(childElements.Where(child => !keyNames.Contains(child.Name.LocalName)));

        if (childElements.SequenceEqual(promotedElements))
        {
            return;
        }

        for (var childIndex = 0; childIndex < childElements.Count; childIndex++)
        {
            childElements[childIndex].ReplaceWith(new XElement(promotedElements[childIndex]));
        }
    }

    private static string[] GetPromotedKeyNames(IReadOnlyList<SortKey> keys)
    {
        var keyNames = new List<string>(keys.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (key.Kind == SortKeyKind.Element && seen.Add(key.Name))
            {
                keyNames.Add(key.Name);
            }
        }

        return keyNames.ToArray();
    }

    private static bool IsTargetElementMatch(
        XElement element,
        XmlPathPatternMatcher.SegmentMatcher targetMatcher,
        Dictionary<string, bool> targetMatchCache)
    {
        var localName = element.Name.LocalName;

        if (targetMatchCache.TryGetValue(localName, out var isMatch))
        {
            return isMatch;
        }

        isMatch = targetMatcher.IsMatch(localName);
        targetMatchCache.Add(localName, isMatch);
        return isMatch;
    }

    private static IEnumerable<XElement> SortElements(IReadOnlyList<XElement> elements, IReadOnlyList<SortKey> keys)
    {
        var sortableElements = CreateSortableElements(elements, keys);
        IOrderedEnumerable<SortableElement>? ordered = null;

        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var comparer = new ElementSortKeyComparer(keys[keyIndex], keyIndex);
            ordered = ordered is null
                ? sortableElements.OrderBy(element => element, comparer)
                : ordered.ThenBy(element => element, comparer);
        }

        var sortedElements = ordered is null ? sortableElements.AsEnumerable() : ordered;

        return sortedElements.Select(element => element.Element);
    }

    private static SortableElement[] CreateSortableElements(IReadOnlyList<XElement> elements, IReadOnlyList<SortKey> keys)
    {
        var sortableElements = new SortableElement[elements.Count];

        for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
            var element = elements[elementIndex];
            sortableElements[elementIndex] = new SortableElement(element, GetSortValues(element, keys));
        }

        return sortableElements;
    }

    internal static SortValue[] GetSortValues(XElement element, IReadOnlyList<SortKey> keys)
    {
        var values = new SortValue[keys.Count];

        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];
            var value = GetKeyValue(element, key);
            var numericValue = 0m;
            var isNumeric = key.Numeric && decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out numericValue);

            values[keyIndex] = new SortValue(value, numericValue, isNumeric);
        }

        return values;
    }

    internal static int CompareSortValues(SortValue[] leftValues, SortValue[] rightValues, IReadOnlyList<SortKey> keys)
    {
        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var result = CompareSortValue(leftValues[keyIndex], rightValues[keyIndex], keys[keyIndex]);

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static string GetKeyValue(XElement element, SortKey key)
    {
        return key.Kind == SortKeyKind.Attribute
            ? element.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, key.Name, StringComparison.Ordinal))?.Value ?? string.Empty
            : element.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, key.Name, StringComparison.Ordinal))?.Value ?? string.Empty;
    }

    private sealed class ElementSortKeyComparer(SortKey key, int keyIndex) : IComparer<SortableElement>
    {
        public int Compare(SortableElement? left, SortableElement? right)
        {
            var leftValue = left is null ? SortValue.Empty : left.Values[keyIndex];
            var rightValue = right is null ? SortValue.Empty : right.Values[keyIndex];

            return CompareSortValue(leftValue, rightValue, key);
        }
    }

    private sealed record SortableElement(XElement Element, SortValue[] Values);

    private static int CompareSortValue(SortValue leftValue, SortValue rightValue, SortKey key)
    {
        var result = key.Numeric
            ? CompareNumericValues(leftValue, rightValue)
            : StringComparer.OrdinalIgnoreCase.Compare(leftValue.Text, rightValue.Text);

        return key.Direction == SortDirection.Descending ? -result : result;
    }

    private static int CompareNumericValues(SortValue leftValue, SortValue rightValue)
    {
        if (leftValue.IsNumeric && rightValue.IsNumeric)
        {
            var numericComparison = leftValue.NumericValue.CompareTo(rightValue.NumericValue);

            return numericComparison != 0
                ? numericComparison
                : StringComparer.OrdinalIgnoreCase.Compare(leftValue.Text, rightValue.Text);
        }

        if (leftValue.IsNumeric != rightValue.IsNumeric)
        {
            return leftValue.IsNumeric ? -1 : 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(leftValue.Text, rightValue.Text);
    }

    internal readonly record struct SortValue(string Text, decimal NumericValue, bool IsNumeric)
    {
        public static SortValue Empty { get; } = new(string.Empty, 0, false);
    }

}
