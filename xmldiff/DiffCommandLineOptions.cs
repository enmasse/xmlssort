internal sealed record DiffCommandLineOptions(
    string LeftPath,
    string RightPath,
    IReadOnlyList<SortRule> SortRules,
    bool FormatXml,
    bool FormatJson,
    bool SortByTagName,
    bool ShowHelp,
    string? OutputPath,
    DiffReportFormat? ReportFormat)
{
    public DiffReportFormat EffectiveReportFormat => ReportFormat
        ?? (OutputPath is not null && OutputPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? DiffReportFormat.Html
            : DiffReportFormat.Text);

    public static DiffCommandLineOptions Parse(string[] args)
    {
        var inputPaths = new List<string>();
        var sortRules = new List<SortRule>();
        var formatXml = false;
        var formatJson = false;
        var sortByTagName = false;
        var showHelp = false;
        string? outputPath = null;
        DiffReportFormat? reportFormat = null;

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

            if (string.Equals(arg, "--sort-tags", StringComparison.OrdinalIgnoreCase))
            {
                sortByTagName = true;
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

            if (arg.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = arg[9..];
                continue;
            }

            if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = ReadOptionValue(args, ref i, arg);
                continue;
            }

            if (arg.StartsWith("--report-format=", StringComparison.OrdinalIgnoreCase))
            {
                reportFormat = ParseReportFormat(arg[16..]);
                continue;
            }

            if (string.Equals(arg, "--report-format", StringComparison.OrdinalIgnoreCase))
            {
                reportFormat = ParseReportFormat(ReadOptionValue(args, ref i, arg));
                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option '{arg}'.");
            }

            inputPaths.Add(arg);
        }

        Validate(inputPaths, outputPath, showHelp);

        return new DiffCommandLineOptions(
            inputPaths.Count > 0 ? inputPaths[0] : string.Empty,
            inputPaths.Count > 1 ? inputPaths[1] : string.Empty,
            sortRules,
            formatXml,
            formatJson,
            sortByTagName,
            showHelp,
            outputPath,
            reportFormat);
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

    private static DiffReportFormat ParseReportFormat(string value)
    {
        if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
        {
            return DiffReportFormat.Text;
        }

        if (string.Equals(value, "html", StringComparison.OrdinalIgnoreCase))
        {
            return DiffReportFormat.Html;
        }

        throw new ArgumentException($"Invalid report format '{value}'. Expected 'text' or 'html'.");
    }

    private static void Validate(IReadOnlyList<string> inputPaths, string? outputPath, bool showHelp)
    {
        if (showHelp)
        {
            return;
        }

        if (inputPaths.Count != 2)
        {
            throw new ArgumentException("xmldiff requires exactly two input XML files.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        if (outputPath != "-" && string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("The --output option requires a non-empty value.");
        }
    }
}
