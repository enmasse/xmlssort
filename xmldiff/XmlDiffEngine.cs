using System.Globalization;
using System.Xml.Linq;

internal static class XmlDiffEngine
{
    private sealed record TargetMatch(XElement Element, string ScopePath, string[] ElementPathSegments);

    private sealed record ParentMatch(XElement Element, string[] ScopePathSegments, string[] PathSegments);

    public static XmlDiffReport Compare(XDocument leftDocument, XDocument rightDocument, IReadOnlyList<SortRule> rules, bool includeComments = false)
    {
        var reports = rules
            .Select(rule => CompareRule(leftDocument, rightDocument, rule, includeComments))
            .ToArray();

        return new XmlDiffReport(reports, CommentsIgnored: !includeComments);
    }

    private static RuleDiffReport CompareRule(XDocument leftDocument, XDocument rightDocument, SortRule rule, bool includeComments)
    {
        var leftTargets = CollectTargets(leftDocument, rule);
        var rightTargets = CollectTargets(rightDocument, rule);
        var leftLookup = BuildLookup(leftTargets, rule.Keys);
        var rightLookup = BuildLookup(rightTargets, rule.Keys);
        var duplicateKeys = leftLookup.Keys
            .Union(rightLookup.Keys, StringComparer.Ordinal)
            .Where(key => leftLookup.GetValueOrDefault(key)?.Count > 1 || rightLookup.GetValueOrDefault(key)?.Count > 1)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new DuplicateKeyConflict(
                key,
                CloneElements(leftLookup.GetValueOrDefault(key)?.Select(match => match.Element).ToArray()),
                CloneElements(rightLookup.GetValueOrDefault(key)?.Select(match => match.Element).ToArray())))
            .ToArray();
        var duplicateKeySet = new HashSet<string>(duplicateKeys.Select(conflict => conflict.Key), StringComparer.Ordinal);
        var differences = new List<KeyedElementDifference>();
        var matchedCount = 0;
        var allKeys = leftLookup.Keys
            .Union(rightLookup.Keys, StringComparer.Ordinal)
            .Where(key => !duplicateKeySet.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in allKeys)
        {
            var hasLeft = leftLookup.TryGetValue(key, out var leftMatches);
            var hasRight = rightLookup.TryGetValue(key, out var rightMatches);

            if (hasLeft && hasRight)
            {
                matchedCount++;
                var leftComparable = CreateComparableElement(leftMatches![0].Element, includeComments);
                var rightComparable = CreateComparableElement(rightMatches![0].Element, includeComments);

                if (!XNode.DeepEquals(leftComparable, rightComparable))
                {
                    differences.Add(new KeyedElementDifference(KeyedDifferenceKind.Changed, key, leftComparable, rightComparable, leftMatches[0].ElementPathSegments));
                }

                continue;
            }

            differences.Add(new KeyedElementDifference(
                hasLeft ? KeyedDifferenceKind.OnlyInLeft : KeyedDifferenceKind.OnlyInRight,
                key,
                hasLeft ? CreateComparableElement(leftMatches![0].Element, includeComments) : null,
                hasRight ? CreateComparableElement(rightMatches![0].Element, includeComments) : null,
                hasLeft ? leftMatches![0].ElementPathSegments : rightMatches![0].ElementPathSegments));
        }

        return new RuleDiffReport(rule, differences, duplicateKeys, matchedCount);
    }

    private static Dictionary<string, List<TargetMatch>> BuildLookup(IEnumerable<TargetMatch> elements, IReadOnlyList<SortKey> keys)
    {
        var lookup = new Dictionary<string, List<TargetMatch>>(StringComparer.Ordinal);

        foreach (var element in elements)
        {
            var key = BuildScopedCompositeKey(element, keys);

            if (!lookup.TryGetValue(key, out var matches))
            {
                matches = [];
                lookup.Add(key, matches);
            }

            matches.Add(element);
        }

        return lookup;
    }

    private static string BuildScopedCompositeKey(TargetMatch targetMatch, IReadOnlyList<SortKey> keys)
    {
        var compositeKey = BuildCompositeKey(targetMatch.Element, keys);
        return string.IsNullOrEmpty(targetMatch.ScopePath)
            ? compositeKey
            : $"{targetMatch.ScopePath} | {compositeKey}";
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
            var comparableValue = key.Numeric && value.IsNumeric
                ? value.NumericValue.ToString(CultureInfo.InvariantCulture)
                : value.Text;
            parts[keyIndex] = $"{displayName}={comparableValue}";
        }

        return string.Join(", ", parts);
    }

    private static List<TargetMatch> CollectTargets(XDocument document, SortRule rule)
    {
        if (document.Root is null)
        {
            return [];
        }

        var compiledRule = new CompiledSortRule(rule);
        var topLevelParents = new List<ParentMatch>();
        var currentPath = new List<string>(8);
        CollectTopLevelParents(document.Root, compiledRule.ParentPathMatcher, false, currentPath, [FormatIndexedSegment(document.Root.Name.LocalName, 1)], topLevelParents);
        var targets = new List<TargetMatch>();

        foreach (var parent in topLevelParents)
        {
            CollectTargetsRecursively(parent.Element, compiledRule.ParentMatcher, compiledRule.TargetMatcher, parent.ScopePathSegments, parent.PathSegments, targets);
        }

        return targets;
    }

    private static void CollectTopLevelParents(
        XElement current,
        XmlPathPatternMatcher.PathMatcher parentPathMatcher,
        bool isWithinMatchedParent,
        List<string> currentPath,
        List<string> currentScopePath,
        List<ParentMatch> topLevelParents)
    {
        currentPath.Add(current.Name.LocalName);

        var isMatchedParent = parentPathMatcher.IsMatch(currentPath);

        if (isMatchedParent && !isWithinMatchedParent)
        {
            topLevelParents.Add(new ParentMatch(current, currentScopePath.ToArray(), currentPath.ToArray()));
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        var childOccurrenceIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var child in current.Elements())
        {
            var childName = child.Name.LocalName;
            childOccurrenceIndexes.TryGetValue(childName, out var currentIndex);
            var childScopePath = new List<string>(currentScopePath)
            {
                FormatIndexedSegment(childName, currentIndex + 1)
            };
            childOccurrenceIndexes[childName] = currentIndex + 1;
            CollectTopLevelParents(child, parentPathMatcher, isWithinMatchedParent, currentPath, childScopePath, topLevelParents);
        }

        currentPath.RemoveAt(currentPath.Count - 1);
    }

    private static void CollectTargetsRecursively(
        XElement current,
        XmlPathPatternMatcher.SegmentMatcher parentMatcher,
        XmlPathPatternMatcher.SegmentMatcher targetMatcher,
        IReadOnlyList<string> currentScopePath,
        IReadOnlyList<string> currentPathSegments,
        List<TargetMatch> targets)
    {
        if (parentMatcher.IsMatch(current.Name.LocalName))
        {
            var targetMatchCache = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var element in current.Elements().Where(element => IsTargetElementMatch(element, targetMatcher, targetMatchCache)))
            {
                var elementPathSegments = new string[currentPathSegments.Count + 1];

                for (var pathIndex = 0; pathIndex < currentPathSegments.Count; pathIndex++)
                {
                    elementPathSegments[pathIndex] = currentPathSegments[pathIndex];
                }

                elementPathSegments[^1] = element.Name.LocalName;
                targets.Add(new TargetMatch(element, string.Join('/', currentScopePath), elementPathSegments));
            }
        }

        var childOccurrenceIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var child in current.Elements())
        {
            var childName = child.Name.LocalName;
            childOccurrenceIndexes.TryGetValue(childName, out var currentIndex);
            var childScopePath = new List<string>(currentScopePath)
            {
                FormatIndexedSegment(childName, currentIndex + 1)
            };
            childOccurrenceIndexes[childName] = currentIndex + 1;
            var childPathSegments = new List<string>(currentPathSegments)
            {
                childName
            };
            CollectTargetsRecursively(child, parentMatcher, targetMatcher, childScopePath, childPathSegments, targets);
        }
    }

    private static string FormatIndexedSegment(string localName, int occurrenceIndex)
    {
        return $"{localName}[{occurrenceIndex}]";
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

    private static IReadOnlyList<XElement> CloneElements(IReadOnlyList<XElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            return [];
        }

        return elements.Select(element => CreateComparableElement(element, includeComments: false)).ToArray();
    }

    private static XElement CreateComparableElement(XElement element, bool includeComments)
    {
        var clone = new XElement(element.Name, element.Attributes().Select(attribute => new XAttribute(attribute)));
        var hasElementChildren = element.Elements().Any();

        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement childElement:
                    clone.Add(CreateComparableElement(childElement, includeComments));
                    break;
                case XCData cdata:
                    clone.Add(new XCData(cdata.Value));
                    break;
                case XText text when !hasElementChildren || !string.IsNullOrWhiteSpace(text.Value):
                    clone.Add(new XText(text.Value));
                    break;
                case XProcessingInstruction instruction:
                    clone.Add(new XProcessingInstruction(instruction.Target, instruction.Data));
                    break;
                case XComment comment when includeComments:
                    clone.Add(new XComment(comment.Value));
                    break;
            }
        }

        return clone;
    }
}
