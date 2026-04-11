internal sealed record CommandLineOptions(
    string? InputPath,
    string? OutputPath,
    IReadOnlyList<SortRule> SortRules,
    bool FormatXml,
    bool FormatJson,
    bool ShowHelp)
{
    public static CommandLineOptions Parse(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        var sortRules = new List<SortRule>();
        var formatXml = false;
        var formatJson = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                continue;
            }

            if (arg.StartsWith("--sort=", StringComparison.OrdinalIgnoreCase))
            {
                sortRules.Add(SortRule.Parse(arg[7..]));
                continue;
            }

            if (string.Equals(arg, "--sort", StringComparison.OrdinalIgnoreCase))
            {
                sortRules.Add(SortRule.Parse(ReadOptionValue(args, ref i, "--sort")));
                continue;
            }

            if (arg.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = arg[9..];
                continue;
            }

            if (string.Equals(arg, "--format-json", StringComparison.OrdinalIgnoreCase))
            {
                formatJson = true;
                continue;
            }

            if (string.Equals(arg, "--format-xml", StringComparison.OrdinalIgnoreCase))
            {
                formatXml = true;
                continue;
            }

            if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = ReadOptionValue(args, ref i, arg);
                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option '{arg}'.");
            }

            if (inputPath is not null)
            {
                throw new ArgumentException("Only one input file may be supplied.");
            }

            inputPath = arg;
        }

        return new CommandLineOptions(inputPath, outputPath, sortRules, formatXml, formatJson, showHelp);
    }

    private static string ReadOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{optionName}'.");
        }

        index++;
        return args[index];
    }
}
