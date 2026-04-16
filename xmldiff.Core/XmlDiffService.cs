using System.Xml.Linq;

public sealed class XmlDiffService
{
    public XmlDiffRequest LoadDefaults(string? configurationPath = null)
    {
        var userConfiguration = LoadConfiguration(configurationPath);

        if (userConfiguration is null)
        {
            return XmlDiffRequest.Empty;
        }

        return new XmlDiffRequest(
            string.Empty,
            string.Empty,
            userConfiguration.SortRules.Select(SortRuleFormatter.Format).ToArray(),
            userConfiguration.FormatXml,
            userConfiguration.FormatJson,
            userConfiguration.SortByTagName);
    }

    public XmlDiffExecutionResult Generate(XmlDiffRequest request, string? configurationPath = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.LeftPath) || string.IsNullOrWhiteSpace(request.RightPath))
        {
            throw new ArgumentException("Both left and right XML files are required.");
        }

        var userConfiguration = LoadConfiguration(configurationPath);
        var sortRules = ResolveSortRules(request.SortRules, userConfiguration);
        var formatXml = request.FormatXml || userConfiguration?.FormatXml == true;
        var formatJson = request.FormatJson || userConfiguration?.FormatJson == true;
        var sortByTagName = request.SortByTagName || userConfiguration?.SortByTagName == true;
        var effectiveOptions = new XmlDiffRequest(
            request.LeftPath,
            request.RightPath,
            sortRules.Select(SortRuleFormatter.Format).ToArray(),
            formatXml,
            formatJson,
            sortByTagName);
        var leftDocument = LoadDocument(request.LeftPath, sortRules, sortByTagName, formatJson);
        var rightDocument = LoadDocument(request.RightPath, sortRules, sortByTagName, formatJson);
        var report = XmlDiffEngine.Compare(leftDocument, rightDocument, sortRules);

        return new XmlDiffExecutionResult(
            effectiveOptions,
            TextDiffReportWriter.Write(report, formatXml),
            HtmlDiffReportWriter.Write(report));
    }

    private static UserConfiguration? LoadConfiguration(string? configurationPath)
    {
        return new UserProfileConfigurationLoader(configurationPath).Load();
    }

    private static IReadOnlyList<SortRule> ResolveSortRules(IReadOnlyList<string> explicitSortRules, UserConfiguration? userConfiguration)
    {
        var parsedRules = explicitSortRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(SortRule.Parse)
            .ToArray();

        if (parsedRules.Length > 0)
        {
            return parsedRules;
        }

        if (userConfiguration?.SortRules.Count > 0)
        {
            return userConfiguration.SortRules;
        }

        throw new ArgumentException("At least one keyed sort rule is required. Supply --sort or configure shared sort rules in the xmlssort config file.");
    }

    private static XDocument LoadDocument(string path, IReadOnlyList<SortRule> sortRules, bool sortByTagName, bool formatJson)
    {
        using var stream = File.OpenRead(path);
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        return XmlDocumentProcessor.Normalize(document, sortRules, sortByTagName, formatJson);
    }
}
