using System.Xml.Linq;

internal sealed class XmlSortApplication(IUserConfigurationLoader? userConfigurationLoader = null)
{
    public int Run(string[] args)
    {
        CommandLineOptions options;

        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CommandLineHelp.Text);
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.Out.WriteLine(CommandLineHelp.Text);
            return 0;
        }

        try
        {
            var userConfiguration = (userConfigurationLoader ?? new UserProfileConfigurationLoader()).Load();
            options = CommandLineOptionsResolver.Resolve(options, userConfiguration);

            var inputPaths = InputFileResolver.Resolve(options.InputPaths);

            if (inputPaths.Count > 1 || options.BatchOutputMode is not BatchOutputMode.None)
            {
                ProcessInputFiles(options, inputPaths);
                return 0;
            }

            var input = ReadInput(inputPaths.Count == 1 && !inputPaths[0].IsStandardInput ? inputPaths[0].InputPath : null);
            var document = ProcessDocument(input, options);
            WriteOutput(document, options.OutputPath, options.FormatXml);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string ReadInput(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || inputPath == "-")
        {
            var stdin = Console.In.ReadToEnd();

            if (string.IsNullOrWhiteSpace(stdin))
            {
                throw new ArgumentException("No XML input was provided. Supply a file path or pipe XML through stdin.");
            }

            return stdin;
        }

        return File.ReadAllText(inputPath);
    }

    private static void ProcessInputFiles(CommandLineOptions options, IReadOnlyList<InputFileMatch> inputPaths)
    {
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("No input files were supplied. Supply a file path, directory, glob pattern, or pipe XML through stdin.");
        }

        if (options.BatchOutputMode is BatchOutputMode.None)
        {
            throw new ArgumentException("Multiple input files require --in-place, --rename, or --write-new.");
        }

        foreach (var inputPath in inputPaths)
        {
            if (inputPath.IsStandardInput)
            {
                throw new ArgumentException("The '-' input can only be used by itself without batch output options.");
            }

            var document = ProcessDocument(File.ReadAllText(inputPath.InputPath), options);

            switch (options.BatchOutputMode)
            {
                case BatchOutputMode.InPlace:
                    WriteOutput(document, inputPath.InputPath, options.FormatXml);
                    break;
                case BatchOutputMode.Rename:
                {
                    var outputPath = BatchOutputPathResolver.ResolveSibling(inputPath.InputPath, options.Suffix!);
                    WriteOutput(document, outputPath, options.FormatXml);
                    File.Delete(inputPath.InputPath);
                    break;
                }
                case BatchOutputMode.WriteNew:
                {
                    var outputPath = options.OutputDirectory is null
                        ? BatchOutputPathResolver.ResolveSibling(inputPath.InputPath, options.Suffix!)
                        : BatchOutputPathResolver.ResolveOutputDirectoryPath(inputPath, Path.GetFullPath(options.OutputDirectory), options.Suffix!);
                    WriteOutput(document, outputPath, options.FormatXml);
                    break;
                }
            }
        }
    }

    private static XDocument ProcessDocument(string input, CommandLineOptions options)
    {
        var document = XDocument.Parse(input, LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, options.SortRules);

        if (options.FormatJson)
        {
            EmbeddedJsonFormatter.Apply(document);
        }

        return document;
    }

    private static void WriteOutput(XDocument document, string? outputPath, bool formatXml)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || outputPath == "-")
        {
            WriteDocument(document, Console.Out, formatXml);
            return;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var writer = new StreamWriter(outputPath);
        WriteDocument(document, writer, formatXml);
    }

    private static void WriteDocument(XDocument document, TextWriter writer, bool formatXml)
    {
        if (formatXml)
        {
            CanonicalXmlFormatter.Write(document, writer);
            return;
        }

        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
