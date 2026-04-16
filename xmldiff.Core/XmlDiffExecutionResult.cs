public sealed record XmlDiffExecutionResult(
    XmlDiffRequest EffectiveOptions,
    string TextReport,
    string HtmlReport);
