internal static class SortRuleFormatter
{
    public static string Format(SortRule rule)
    {
        var path = "/" + string.Join('/', rule.PathSegments);
        var keys = string.Join(',', rule.Keys.Select(Format));
        return $"{path}:{keys}";
    }

    public static string Format(SortKey key)
    {
        var name = key.Kind == SortKeyKind.Attribute ? $"@{key.Name}" : key.Name;

        if (key.Numeric)
        {
            name += " numeric";
        }

        if (key.Direction == SortDirection.Descending)
        {
            name += " desc";
        }

        return name;
    }
}
