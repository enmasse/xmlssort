using System.Xml.Linq;

internal sealed record KeyedElementDifference(
    KeyedDifferenceKind Kind,
    string Key,
    XElement? LeftElement,
    XElement? RightElement,
    IReadOnlyList<string> TargetPathSegments);

internal sealed record DuplicateKeyConflict(
    string Key,
    IReadOnlyList<XElement> LeftElements,
    IReadOnlyList<XElement> RightElements);

internal sealed record RuleDiffReport(
    SortRule Rule,
    IReadOnlyList<KeyedElementDifference> Differences,
    IReadOnlyList<DuplicateKeyConflict> DuplicateKeys,
    int MatchedCount)
{
    public int ChangedCount => Differences.Count(difference => difference.Kind == KeyedDifferenceKind.Changed);

    public int OnlyInLeftCount => Differences.Count(difference => difference.Kind == KeyedDifferenceKind.OnlyInLeft);

    public int OnlyInRightCount => Differences.Count(difference => difference.Kind == KeyedDifferenceKind.OnlyInRight);
}

internal sealed record XmlDiffReport(IReadOnlyList<RuleDiffReport> Rules, bool CommentsIgnored)
{
    public int ChangedCount => Rules.Sum(rule => rule.ChangedCount);

    public int OnlyInLeftCount => Rules.Sum(rule => rule.OnlyInLeftCount);

    public int OnlyInRightCount => Rules.Sum(rule => rule.OnlyInRightCount);

    public int DuplicateKeyCount => Rules.Sum(rule => rule.DuplicateKeys.Count);

    public int MatchedCount => Rules.Sum(rule => rule.MatchedCount);

    public bool HasDifferences => ChangedCount > 0 || OnlyInLeftCount > 0 || OnlyInRightCount > 0 || DuplicateKeyCount > 0;
}
