internal sealed record SortRule(IReadOnlyList<string> PathSegments, IReadOnlyList<SortKey> Keys)
{
    public string TargetElementName => PathSegments[^1];

    public static SortRule Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sort rules cannot be empty.");
        }

        var separatorIndex = value.IndexOf(':');

        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            throw new ArgumentException($"Invalid sort rule '{value}'. Expected '/Root/Parent/Item:key1,key2'.");
        }

        var path = value[..separatorIndex].Trim();
        var keyExpression = value[(separatorIndex + 1)..].Trim();

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid sort path '{path}'. Sort paths must start with '/'.");
        }

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (pathSegments.Length < 2)
        {
            throw new ArgumentException($"Invalid sort path '{path}'. The path must include a root and target element.");
        }

        var keys = keyExpression
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SortKey.Parse)
            .ToArray();

        if (keys.Length == 0)
        {
            throw new ArgumentException($"Sort rule '{value}' must include at least one key.");
        }

        return new SortRule(pathSegments, keys);
    }
}
