using System.Xml.Linq;

internal static class XmlSorter
{
    public static void Apply(XDocument document, IReadOnlyList<SortRule> rules)
    {
        if (document.Root is null)
        {
            throw new ArgumentException("The XML document does not have a root element.");
        }

        foreach (var rule in rules)
        {
            ApplyRule(document.Root, rule);
        }
    }

    private static void ApplyRule(XElement root, SortRule rule)
    {
        if (!string.Equals(root.Name.LocalName, rule.PathSegments[0], StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Sort path '/{string.Join('/', rule.PathSegments)}' does not match the document root '{root.Name.LocalName}'.");
        }

        IEnumerable<XElement> parents = [root];

        for (var i = 1; i < rule.PathSegments.Count - 1; i++)
        {
            var segment = rule.PathSegments[i];
            parents = parents.SelectMany(parent => parent.Elements().Where(child => string.Equals(child.Name.LocalName, segment, StringComparison.Ordinal)));
        }

        foreach (var parent in parents)
        {
            SortChildrenRecursively(parent, rule.PathSegments[^2], rule.TargetElementName, rule.Keys);
        }
    }

    private static void SortChildrenRecursively(XElement current, string parentElementName, string targetElementName, IReadOnlyList<SortKey> keys)
    {
        if (string.Equals(current.Name.LocalName, parentElementName, StringComparison.Ordinal))
        {
            SortChildren(current, targetElementName, keys);
        }

        foreach (var child in current.Elements().ToList())
        {
            SortChildrenRecursively(child, parentElementName, targetElementName, keys);
        }
    }

    private static void SortChildren(XElement parent, string targetElementName, IReadOnlyList<SortKey> keys)
    {
        var targetElements = parent.Elements()
            .Where(element => string.Equals(element.Name.LocalName, targetElementName, StringComparison.Ordinal))
            .ToList();

        if (targetElements.Count < 2)
        {
            return;
        }

        var sortedTargets = new Queue<XElement>(
            SortElements(targetElements, keys)
                .Select(element => new XElement(element)));

        var replacementNodes = new List<XNode>();

        foreach (var node in parent.Nodes())
        {
            if (node is XElement element && string.Equals(element.Name.LocalName, targetElementName, StringComparison.Ordinal))
            {
                replacementNodes.Add(sortedTargets.Dequeue());
                continue;
            }

            replacementNodes.Add(CloneNode(node));
        }

        parent.ReplaceNodes(replacementNodes);
    }

    private static IEnumerable<XElement> SortElements(IReadOnlyList<XElement> elements, IReadOnlyList<SortKey> keys)
    {
        IOrderedEnumerable<XElement>? ordered = null;

        foreach (var key in keys)
        {
            ordered = ordered is null
                ? ApplyPrimaryOrdering(elements, key)
                : ApplySecondaryOrdering(ordered, key);
        }

        return ordered ?? elements.AsEnumerable();
    }

    private static IOrderedEnumerable<XElement> ApplyPrimaryOrdering(IEnumerable<XElement> elements, SortKey key)
    {
        return key.Direction == SortDirection.Ascending
            ? elements.OrderBy(element => GetKeyValue(element, key), StringComparer.OrdinalIgnoreCase)
            : elements.OrderByDescending(element => GetKeyValue(element, key), StringComparer.OrdinalIgnoreCase);
    }

    private static IOrderedEnumerable<XElement> ApplySecondaryOrdering(IOrderedEnumerable<XElement> elements, SortKey key)
    {
        return key.Direction == SortDirection.Ascending
            ? elements.ThenBy(element => GetKeyValue(element, key), StringComparer.OrdinalIgnoreCase)
            : elements.ThenByDescending(element => GetKeyValue(element, key), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetKeyValue(XElement element, SortKey key)
    {
        return key.Kind == SortKeyKind.Attribute
            ? element.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, key.Name, StringComparison.Ordinal))?.Value ?? string.Empty
            : element.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, key.Name, StringComparison.Ordinal))?.Value ?? string.Empty;
    }

    private static XNode CloneNode(XNode node)
    {
        return node switch
        {
            XElement element => new XElement(element),
            XCData cdata => new XCData(cdata.Value),
            XText text => new XText(text.Value),
            XComment comment => new XComment(comment.Value),
            XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data),
            XDocumentType documentType => new XDocumentType(documentType.Name, documentType.PublicId, documentType.SystemId, documentType.InternalSubset),
            _ => throw new InvalidOperationException($"Unsupported XML node type '{node.GetType().Name}'.")
        };
    }
}
