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

            ProcessSingleInput(
                inputPaths.Count == 1 && !inputPaths[0].IsStandardInput ? inputPaths[0].InputPath : null,
                options.OutputPath,
                options);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static XDocument LoadDocument(string? inputPath, CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || inputPath == "-")
        {
            return ProcessDocument(XDocument.Parse(ReadStandardInput(), LoadOptions.PreserveWhitespace), options);
        }

        using var stream = File.OpenRead(inputPath);
        return ProcessDocument(XDocument.Load(stream, LoadOptions.PreserveWhitespace), options);
    }

    private static string ReadStandardInput()
    {
        var stdin = Console.In.ReadToEnd();

        if (string.IsNullOrWhiteSpace(stdin))
        {
            throw new ArgumentException("No XML input was provided. Supply a file path or pipe XML through stdin.");
        }

        return stdin;
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

            switch (options.BatchOutputMode)
            {
                case BatchOutputMode.InPlace:
                    ProcessSingleInput(inputPath.InputPath, inputPath.InputPath, options);
                    break;
                case BatchOutputMode.Rename:
                {
                    var outputPath = BatchOutputPathResolver.ResolveSibling(inputPath.InputPath, options.Suffix!);
                    ProcessSingleInput(inputPath.InputPath, outputPath, options);
                    File.Delete(inputPath.InputPath);
                    break;
                }
                case BatchOutputMode.WriteNew:
                {
                    var outputPath = options.OutputDirectory is null
                        ? BatchOutputPathResolver.ResolveSibling(inputPath.InputPath, options.Suffix!)
                        : BatchOutputPathResolver.ResolveOutputDirectoryPath(inputPath, Path.GetFullPath(options.OutputDirectory), options.Suffix!);
                    ProcessSingleInput(inputPath.InputPath, outputPath, options);
                    break;
                }
            }
        }
    }

    private static void ProcessSingleInput(string? inputPath, string? outputPath, CommandLineOptions options)
    {
        if (XmlStreamingProcessor.CanProcess(options))
        {
            ProcessStreaming(inputPath, outputPath, options);
            return;
        }

        var document = LoadDocument(inputPath, options);
        WriteOutput(document, outputPath, options.FormatXml);
    }

    private static void ProcessStreaming(string? inputPath, string? outputPath, CommandLineOptions options)
    {
        if (!string.IsNullOrWhiteSpace(inputPath)
            && !string.IsNullOrWhiteSpace(outputPath)
            && string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            var tempOutputPath = Path.Combine(
                Path.GetDirectoryName(outputPath)!,
                $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                XmlStreamingProcessor.Process(inputPath, tempOutputPath, options);
                File.Move(tempOutputPath, outputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempOutputPath))
                {
                    File.Delete(tempOutputPath);
                }
            }

            return;
        }

        XmlStreamingProcessor.Process(inputPath, outputPath, options);
    }

    private static XDocument ProcessDocument(XDocument document, CommandLineOptions options)
    {
        return XmlDocumentProcessor.Normalize(document, options.SortRules, options.SortByTagName, options.FormatJson);
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
