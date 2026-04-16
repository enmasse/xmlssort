using System.Xml.Linq;

internal sealed class XmlDiffApplication(IUserConfigurationLoader? userConfigurationLoader = null)
{
    public int Run(string[] args)
    {
        DiffCommandLineOptions options;

        try
        {
            options = DiffCommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(DiffCommandLineHelp.Text);
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.Out.WriteLine(DiffCommandLineHelp.Text);
            return 0;
        }

        try
        {
            var userConfiguration = (userConfigurationLoader ?? new UserProfileConfigurationLoader()).Load();
            options = DiffCommandLineOptionsResolver.Resolve(options, userConfiguration);
            var leftDocument = LoadDocument(options.LeftPath, options);
            var rightDocument = LoadDocument(options.RightPath, options);
            var report = XmlDiffEngine.Compare(leftDocument, rightDocument, options.SortRules);
            var reportText = options.EffectiveReportFormat == DiffReportFormat.Html
                ? HtmlDiffReportWriter.Write(report)
                : TextDiffReportWriter.Write(report, options.FormatXml);
            WriteOutput(reportText, options.OutputPath);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static XDocument LoadDocument(string path, DiffCommandLineOptions options)
    {
        using var stream = File.OpenRead(path);
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        return XmlDocumentProcessor.Normalize(document, options.SortRules, options.SortByTagName, options.FormatJson);
    }

    private static void WriteOutput(string report, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || outputPath == "-")
        {
            Console.Out.Write(report);
            return;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, report);
    }
}
