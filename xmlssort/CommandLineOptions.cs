internal sealed record CommandLineOptions(
    IReadOnlyList<string> InputPaths,
    string? OutputPath,
    string? OutputDirectory,
    IReadOnlyList<SortRule> SortRules,
    bool FormatXml,
    bool FormatJson,
    bool ShowHelp,
    BatchOutputMode BatchOutputMode,
    string? Suffix,
    bool SortByTagName = false)
{
    public string? InputPath => InputPaths.Count == 1 ? InputPaths[0] : null;

    public static CommandLineOptions Parse(string[] args)
    {
        var inputPaths = new List<string>();
        string? outputPath = null;
        var sortRules = new List<SortRule>();
        var formatXml = false;
        var formatJson = false;
        var showHelp = false;
        var batchOutputMode = BatchOutputMode.None;
        string? outputDirectory = null;
        string? suffix = null;
        var sortByTagName = false;

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

            if (string.Equals(arg, "--sort-tags", StringComparison.OrdinalIgnoreCase))
            {
                sortByTagName = true;
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

            if (arg.StartsWith("--output-dir=", StringComparison.OrdinalIgnoreCase))
            {
                outputDirectory = arg[13..];
                continue;
            }

            if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase))
            {
                outputDirectory = ReadOptionValue(args, ref i, arg);
                continue;
            }

            if (string.Equals(arg, "--in-place", StringComparison.OrdinalIgnoreCase))
            {
                batchOutputMode = ReadBatchOutputMode(batchOutputMode, BatchOutputMode.InPlace, arg);
                continue;
            }

            if (string.Equals(arg, "--rename", StringComparison.OrdinalIgnoreCase))
            {
                batchOutputMode = ReadBatchOutputMode(batchOutputMode, BatchOutputMode.Rename, arg);
                continue;
            }

            if (string.Equals(arg, "--write-new", StringComparison.OrdinalIgnoreCase))
            {
                batchOutputMode = ReadBatchOutputMode(batchOutputMode, BatchOutputMode.WriteNew, arg);
                continue;
            }

            if (arg.StartsWith("--suffix=", StringComparison.OrdinalIgnoreCase))
            {
                suffix = arg[9..];
                continue;
            }

            if (string.Equals(arg, "--suffix", StringComparison.OrdinalIgnoreCase))
            {
                suffix = ReadOptionValue(args, ref i, arg);
                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option '{arg}'.");
            }

            inputPaths.Add(arg);
        }

        Validate(inputPaths, outputPath, outputDirectory, batchOutputMode, suffix, showHelp);

        var effectiveSuffix = batchOutputMode is BatchOutputMode.Rename or BatchOutputMode.WriteNew
            ? suffix ?? BatchOutputPathResolver.DefaultSuffix
            : null;

        return new CommandLineOptions(inputPaths, outputPath, outputDirectory, sortRules, formatXml, formatJson, showHelp, batchOutputMode, effectiveSuffix, sortByTagName);
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

    private static BatchOutputMode ReadBatchOutputMode(BatchOutputMode currentMode, BatchOutputMode nextMode, string optionName)
    {
        if (currentMode is not BatchOutputMode.None)
        {
            throw new ArgumentException($"Only one batch output mode may be supplied. '{optionName}' cannot be combined with another batch output option.");
        }

        return nextMode;
    }

    private static void Validate(
        IReadOnlyList<string> inputPaths,
        string? outputPath,
        string? outputDirectory,
        BatchOutputMode batchOutputMode,
        string? suffix,
        bool showHelp)
    {
        if (showHelp)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(outputPath) && batchOutputMode is not BatchOutputMode.None)
        {
            throw new ArgumentException("The --output option cannot be combined with --in-place, --rename, or --write-new.");
        }

        if (!string.IsNullOrWhiteSpace(outputPath) && !string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("The --output option cannot be combined with --output-dir.");
        }

        if (inputPaths.Count > 1 && !string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("The --output option can only be used with a single input file.");
        }

        if (inputPaths.Count > 1 && batchOutputMode is BatchOutputMode.None)
        {
            throw new ArgumentException("Multiple input files require --in-place, --rename, or --write-new.");
        }

        if (inputPaths.Count == 0 && batchOutputMode is not BatchOutputMode.None)
        {
            throw new ArgumentException("Batch output options require at least one input file.");
        }

        if (inputPaths.Any(path => path == "-") && (batchOutputMode is not BatchOutputMode.None || inputPaths.Count > 1))
        {
            throw new ArgumentException("The '-' input can only be used by itself without batch output options.");
        }

        if (!string.IsNullOrWhiteSpace(outputDirectory) && batchOutputMode is not BatchOutputMode.WriteNew)
        {
            throw new ArgumentException("The --output-dir option can only be used with --write-new.");
        }

        if (!string.IsNullOrWhiteSpace(outputDirectory) && inputPaths.Count == 0)
        {
            throw new ArgumentException("The --output-dir option requires at least one input file or directory.");
        }

        if (suffix is not null && batchOutputMode is not (BatchOutputMode.Rename or BatchOutputMode.WriteNew))
        {
            throw new ArgumentException("The --suffix option can only be used with --rename or --write-new.");
        }

        if (suffix is not null && string.IsNullOrWhiteSpace(suffix))
        {
            throw new ArgumentException("The --suffix option requires a non-empty value.");
        }

        if (outputDirectory is not null && string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("The --output-dir option requires a non-empty value.");
        }
    }
}
