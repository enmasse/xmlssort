public sealed record XmlDiffRequest(
    string LeftPath,
    string RightPath,
    IReadOnlyList<string> SortRules,
    bool FormatXml,
    bool FormatJson,
    bool SortByTagName)
{
    public static XmlDiffRequest Empty { get; } = new(
        string.Empty,
        string.Empty,
        [],
        FormatXml: false,
        FormatJson: false,
        SortByTagName: false);
}
