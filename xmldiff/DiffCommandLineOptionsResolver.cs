internal static class DiffCommandLineOptionsResolver
{
    public static DiffCommandLineOptions Resolve(DiffCommandLineOptions commandLineOptions, UserConfiguration? userConfiguration)
    {
        var sortRules = commandLineOptions.SortRules.Count > 0
            ? commandLineOptions.SortRules
            : userConfiguration?.SortRules ?? [];

        var formatXml = commandLineOptions.FormatXml || userConfiguration?.FormatXml == true;
        var formatJson = commandLineOptions.FormatJson || userConfiguration?.FormatJson == true;
        var sortByTagName = commandLineOptions.SortByTagName || userConfiguration?.SortByTagName == true;

        if (!commandLineOptions.ShowHelp && sortRules.Count == 0)
        {
            throw new ArgumentException("At least one keyed sort rule is required. Supply --sort or configure shared sort rules in the xmlssort config file.");
        }

        return commandLineOptions with
        {
            SortRules = sortRules,
            FormatXml = formatXml,
            FormatJson = formatJson,
            SortByTagName = sortByTagName
        };
    }
}
