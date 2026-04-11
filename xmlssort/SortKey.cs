internal sealed record SortKey(string Name, SortKeyKind Kind, SortDirection Direction)
{
    public static SortKey Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sort keys cannot be empty.");
        }

        var trimmed = value.Trim();
        var direction = SortDirection.Ascending;

        if (trimmed.EndsWith(" desc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Descending;
            trimmed = trimmed[..^5].TrimEnd();
        }
        else if (trimmed.EndsWith(" asc", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4].TrimEnd();
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"Invalid sort key '{value}'.");
        }

        return trimmed[0] == '@'
            ? new SortKey(trimmed[1..], SortKeyKind.Attribute, direction)
            : new SortKey(trimmed, SortKeyKind.Element, direction);
    }
}
