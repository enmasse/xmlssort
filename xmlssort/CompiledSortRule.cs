internal sealed class CompiledSortRule
{
    public CompiledSortRule(SortRule rule)
    {
        ParentPathMatcher = XmlPathPatternMatcher.CreatePathMatcher(rule.PathSegments.Take(rule.PathSegments.Count - 1).ToArray());
        ParentMatcher = XmlPathPatternMatcher.CreateSegmentMatcher(rule.PathSegments[^2]);
        TargetMatcher = XmlPathPatternMatcher.CreateSegmentMatcher(rule.TargetElementName);
        Keys = rule.Keys;
    }

    public IReadOnlyList<SortKey> Keys { get; }

    public XmlPathPatternMatcher.PathMatcher ParentPathMatcher { get; }

    public XmlPathPatternMatcher.SegmentMatcher ParentMatcher { get; }

    public XmlPathPatternMatcher.SegmentMatcher TargetMatcher { get; }
}
