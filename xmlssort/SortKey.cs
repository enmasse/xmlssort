internal sealed record SortKey(string Name, SortKeyKind Kind, SortDirection Direction, bool Numeric)
{
    public static SortKey Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sort keys cannot be empty.");
        }

        var trimmed = value.Trim();
        var direction = SortDirection.Ascending;
        var numeric = false;

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var modifier = parts[i];

            if (string.Equals(modifier, "desc", StringComparison.OrdinalIgnoreCase))
            {
                direction = SortDirection.Descending;
                continue;
            }

            if (string.Equals(modifier, "asc", StringComparison.OrdinalIgnoreCase))
            {
                direction = SortDirection.Ascending;
                continue;
            }

            if (string.Equals(modifier, "numeric", StringComparison.OrdinalIgnoreCase))
            {
                numeric = true;
                continue;
            }

            throw new ArgumentException($"Invalid sort key '{value}'. Unknown modifier '{modifier}'.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"Invalid sort key '{value}'.");
        }

        return name[0] == '@'
            ? new SortKey(name[1..], SortKeyKind.Attribute, direction, numeric)
            : new SortKey(name, SortKeyKind.Element, direction, numeric);
    }
}
