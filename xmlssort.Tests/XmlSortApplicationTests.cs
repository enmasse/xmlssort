using System.Text;
using System.Xml.Linq;

namespace xmlssort.Tests;

public class XmlSortApplicationTests
{
    private static readonly Lock ConsoleLock = new();

    [Test]
    public async Task Run_WritesHelpToStdOut()
    {
        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope();

            exitCode = new XmlSortApplication().Run(["--help"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains("Usage:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Run_ReadsFromStdInAndWritesSortedXmlToStdOut()
    {
        const string input = "<Catalog><Books><Book id=\"2\"><Title>Zebra</Title></Book><Book id=\"1\"><Title>Alpha</Title></Book></Books></Catalog>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication().Run(["--sort", "/Catalog/Books/Book:@id"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var ids = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Run_FormatsEmbeddedJsonWhenGlobalFlagIsApplied()
    {
        const string input = "<Root><Payload>{\"name\":\"Alice\",\"roles\":[\"admin\"]}</Payload></Root>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication().Run(["--format-json"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var payload = document.Root!.Element("Payload")!.Value;

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(payload.Contains('\n', StringComparison.Ordinal)).IsTrue();
        await Assert.That(payload.Contains("\"name\": \"Alice\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Run_FormatsXmlWhenGlobalFlagIsApplied()
    {
        const string input = "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>";

        int exitCode;
        string stdout;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication().Run(["--sort", "/Catalog/Books/Book:@id", "--format-xml"]);
            stdout = consoleScope.StandardOutput;
        }

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains(Environment.NewLine + "  <Books>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(stdout.Contains(Environment.NewLine + "    <Book id=\"1\" />", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Run_UsesConfigurationDefaultsWhenCommandLineOmitsOperations()
    {
        const string input = "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication(new FakeUserConfigurationLoader(new UserConfiguration(
                [SortRule.Parse("/Catalog/Books/Book:@id")],
                FormatXml: false,
                FormatJson: false))).Run([]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var ids = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Run_PrefersCommandLineSortRulesOverConfigurationDefaults()
    {
        const string input = "<Catalog><Books><Book id=\"2\"><Title>Beta</Title></Book><Book id=\"1\"><Title>Zulu</Title></Book><Book id=\"3\"><Title>Alpha</Title></Book></Books></Catalog>";

        int exitCode;
        string stdout;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication(new FakeUserConfigurationLoader(new UserConfiguration(
                [SortRule.Parse("/Catalog/Books/Book:Title desc")],
                FormatXml: false,
                FormatJson: false))).Run(["--sort", "/Catalog/Books/Book:@id"]);
            stdout = consoleScope.StandardOutput;
        }

        var document = XDocument.Parse(stdout);
        var ids = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2|3");
    }

    [Test]
    public async Task Run_UsesConfigurationDefaultForFormatXml()
    {
        const string input = "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>";

        int exitCode;
        string stdout;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication(new FakeUserConfigurationLoader(new UserConfiguration(
                [SortRule.Parse("/Catalog/Books/Book:@id")],
                FormatXml: true,
                FormatJson: false))).Run([]);
            stdout = consoleScope.StandardOutput;
        }

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains(Environment.NewLine + "  <Books>", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Run_WritesSortedXmlToOutputFile()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.xml");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.xml");

            await File.WriteAllTextAsync(inputPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");

            var exitCode = new XmlSortApplication().Run([inputPath, "--sort", "/Catalog/Books/Book:@id", "--output", outputPath]);
            var output = await File.ReadAllTextAsync(outputPath);

            var document = XDocument.Parse(output);
            var ids = document.Root!
                .Element("Books")!
                .Elements("Book")
                .Select(book => book.Attribute("id")!.Value)
                .ToArray();

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_ReturnsFailureForInvalidXml()
    {
        int exitCode;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope("<Catalog>");

            exitCode = new XmlSortApplication().Run(["--sort", "/Catalog/Books/Book:@id"]);
            stderr = consoleScope.StandardError;
        }

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(stderr.Contains("unexpected end", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    private sealed class ConsoleScope : IDisposable
    {
        private readonly TextReader _originalIn;
        private readonly TextWriter _originalOut;
        private readonly TextWriter _originalError;
        private readonly StringWriter _stdoutWriter;
        private readonly StringWriter _stderrWriter;

        public ConsoleScope(string standardInput = "")
        {
            _originalIn = Console.In;
            _originalOut = Console.Out;
            _originalError = Console.Error;
            _stdoutWriter = new StringWriter(new StringBuilder());
            _stderrWriter = new StringWriter(new StringBuilder());

            Console.SetIn(new StringReader(standardInput));
            Console.SetOut(_stdoutWriter);
            Console.SetError(_stderrWriter);
        }

        public string StandardOutput => _stdoutWriter.ToString();

        public string StandardError => _stderrWriter.ToString();

        public void Dispose()
        {
            Console.SetIn(_originalIn);
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);
            _stdoutWriter.Dispose();
            _stderrWriter.Dispose();
        }
    }

    private sealed class FakeUserConfigurationLoader(UserConfiguration? configuration) : IUserConfigurationLoader
    {
        public UserConfiguration? Load()
        {
            return configuration;
        }
    }
}
