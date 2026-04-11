internal static class CommandLineOptionsResolver
{
    public static CommandLineOptions Resolve(CommandLineOptions commandLineOptions, UserConfiguration? userConfiguration)
    {
        var sortRules = commandLineOptions.SortRules.Count > 0
            ? commandLineOptions.SortRules
            : userConfiguration?.SortRules ?? [];

        var formatXml = commandLineOptions.FormatXml || userConfiguration?.FormatXml == true;
        var formatJson = commandLineOptions.FormatJson || userConfiguration?.FormatJson == true;

        if (!commandLineOptions.ShowHelp && sortRules.Count == 0 && !formatXml && !formatJson)
        {
            throw new ArgumentException("At least one operation is required. Supply --sort, --format-xml, and/or --format-json.");
        }

        return commandLineOptions with
        {
            SortRules = sortRules,
            FormatXml = formatXml,
            FormatJson = formatJson
        };
    }
}
