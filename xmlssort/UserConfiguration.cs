internal sealed record UserConfiguration(
    IReadOnlyList<SortRule> SortRules,
    bool FormatXml,
    bool FormatJson,
    bool SortByTagName = false);
