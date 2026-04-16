internal sealed class XmlDiffApplication
{
    private readonly XmlDiffService _service = new();

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
            var result = _service.Generate(new XmlDiffRequest(
                options.LeftPath,
                options.RightPath,
                options.SortRules.Select(SortRuleFormatter.Format).ToArray(),
                options.FormatXml,
                options.FormatJson,
                options.SortByTagName));
            var reportText = options.EffectiveReportFormat == DiffReportFormat.Html
                ? result.HtmlReport
                : result.TextReport;
            WriteOutput(reportText, options.OutputPath);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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
