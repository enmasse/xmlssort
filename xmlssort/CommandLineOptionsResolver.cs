internal static class CommandLineOptionsResolver
{
    public static CommandLineOptions Resolve(CommandLineOptions commandLineOptions, UserConfiguration? userConfiguration)
    {
        var sortRules = commandLineOptions.SortRules.Count > 0
            ? commandLineOptions.SortRules
            : userConfiguration?.SortRules ?? [];

        var formatJson = commandLineOptions.FormatJson || userConfiguration?.FormatJson == true;

        if (!commandLineOptions.ShowHelp && sortRules.Count == 0 && !formatJson)
        {
            throw new ArgumentException("At least one operation is required. Supply --sort and/or --format-json.");
        }

        return commandLineOptions with
        {
            SortRules = sortRules,
            FormatJson = formatJson
        };
    }
}
