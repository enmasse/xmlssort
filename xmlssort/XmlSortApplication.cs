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

            var input = ReadInput(options.InputPath);
            var document = XDocument.Parse(input, LoadOptions.PreserveWhitespace);

            XmlSorter.Apply(document, options.SortRules);

            if (options.FormatJson)
            {
                EmbeddedJsonFormatter.Apply(document);
            }

            WriteOutput(document, options.OutputPath);
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

    private static void WriteOutput(XDocument document, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || outputPath == "-")
        {
            document.Save(Console.Out, SaveOptions.DisableFormatting);
            return;
        }

        using var writer = new StreamWriter(outputPath);
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
