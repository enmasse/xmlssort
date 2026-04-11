internal sealed record UserConfiguration(
    IReadOnlyList<SortRule> SortRules,
    bool FormatJson);
