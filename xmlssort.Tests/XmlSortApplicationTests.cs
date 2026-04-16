using System.Text;
using System.Xml.Linq;

namespace xmlssort.Tests;

public class XmlSortApplicationTests
{
    private static readonly Lock ConsoleLock = new();
    private static readonly Lock WorkingDirectoryLock = new();

    [Test]
    public async Task Run_WritesHelpToStdOut()
    {
        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope();

            exitCode = CreateApplication().Run(["--help"]);
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

            exitCode = CreateApplication().Run(["--sort", "/Catalog/Books/Book:@id"]);
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
    public async Task Run_AppliesMultipleSortRulesAtDifferentLevels()
    {
        const string input = "<Library><Sections><Section><Name>Fiction</Name><Books><Book id=\"2\"><Title>Zebra</Title></Book><Book id=\"1\"><Title>Alpha</Title></Book></Books></Section><Section><Name>Art</Name><Books><Book id=\"4\"><Title>Modern</Title></Book><Book id=\"3\"><Title>Classic</Title></Book></Books></Section></Sections></Library>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = CreateApplication().Run([
                "--sort", "/Library/Sections/Section:Name",
                "--sort", "/Library/Sections/Section/Books/Book:@id"
            ]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var sections = document.Root!
            .Element("Sections")!
            .Elements("Section")
            .ToArray();

        var sectionNames = sections
            .Select(section => section.Element("Name")!.Value)
            .ToArray();

        var bookIds = sections
            .Select(section => string.Join(",", section.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value)))
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", sectionNames)).IsEqualTo("Art|Fiction");
        await Assert.That(string.Join("|", bookIds)).IsEqualTo("3,4|1,2");
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

            exitCode = CreateApplication().Run(["--format-json"]);
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

            exitCode = CreateApplication().Run(["--sort", "/Catalog/Books/Book:@id", "--format-xml"]);
            stdout = consoleScope.StandardOutput;
        }

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains(Environment.NewLine + "  <Books>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(stdout.Contains(Environment.NewLine + "    <Book id=\"1\" />", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Run_FormatsJsonAndXmlWhenBothGlobalFlagsAreApplied()
    {
        const string input = "<Root><Items><Item id=\"2\"/><Item id=\"1\"/></Items><Payload>{\"name\":\"Alice\",\"roles\":[\"admin\"]}</Payload></Root>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = CreateApplication().Run(["--sort", "/Root/Items/Item:@id", "--format-json", "--format-xml"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var itemIds = document.Root!
            .Element("Items")!
            .Elements("Item")
            .Select(item => item.Attribute("id")!.Value)
            .ToArray();
        var payload = document.Root.Element("Payload")!.Value;

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", itemIds)).IsEqualTo("1|2");
        await Assert.That(stdout.Contains(Environment.NewLine + "  <Items>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(payload.Contains('\n', StringComparison.Ordinal)).IsTrue();
        await Assert.That(payload.Contains("\"name\": \"Alice\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
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

            var exitCode = CreateApplication().Run([inputPath, "--sort", "/Catalog/Books/Book:@id", "--output", outputPath]);
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
    public async Task Run_PreservesInterleavedNonTargetNodesWhenSortingFileOutput()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.xml");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.xml");

            await File.WriteAllTextAsync(
                inputPath,
                """
                <Catalog>
                  <Books>
                    <Book id="2"><Title>Beta</Title></Book>
                    <Note importance="high">keep</Note>
                    <Book id="1"><Title>Alpha</Title></Book>
                  </Books>
                </Catalog>
                """);

            var exitCode = CreateApplication().Run([inputPath, "--sort", "/Catalog/Books/Book:@id", "--output", outputPath]);
            var output = XDocument.Parse(await File.ReadAllTextAsync(outputPath), LoadOptions.PreserveWhitespace);
            var books = output.Root!.Element("Books")!;
            var ids = books.Elements("Book")
                .Select(book => book.Attribute("id")!.Value)
                .ToArray();
            var note = books.Element("Note")!;
            var nodes = books.Nodes().ToArray();

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
            await Assert.That(note.Value).IsEqualTo("keep");
            await Assert.That(nodes[0]).IsTypeOf<XText>();
            await Assert.That(nodes[1]).IsTypeOf<XElement>();
            await Assert.That(((XElement)nodes[1]).Name.LocalName).IsEqualTo("Book");
            await Assert.That(nodes[3]).IsTypeOf<XElement>();
            await Assert.That(((XElement)nodes[3]).Name.LocalName).IsEqualTo("Note");
            await Assert.That(nodes[5]).IsTypeOf<XElement>();
            await Assert.That(((XElement)nodes[5]).Name.LocalName).IsEqualTo("Book");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_SortsWildcardFileInputWhilePreservingProcessingInstructions()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.xml");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.xml");

            await File.WriteAllTextAsync(
                inputPath,
                """
                <operations>
                  <add>
                    <product>
                      <product_key>pk-1</product_key>
                      <related_items__alpha>
                        <item><id>210</id></item>
                        <?between stay?>
                        <item><id>110</id></item>
                      </related_items__alpha>
                      <variants>
                        <variant>
                          <id>v-1</id>
                          <related_items__beta>
                            <item><id>430</id></item>
                            <item><id>130</id></item>
                          </related_items__beta>
                        </variant>
                      </variants>
                    </product>
                  </add>
                </operations>
                """);

            var exitCode = CreateApplication().Run([inputPath, "--sort", "/operations/add/product/**/related_items__*/item:id", "--output", outputPath]);
            var output = XDocument.Parse(await File.ReadAllTextAsync(outputPath), LoadOptions.PreserveWhitespace);
            var product = output.Root!
                .Element("add")!
                .Element("product")!;
            var relatedItems = product.Element("related_items__alpha")!;
            var directIds = relatedItems.Elements("item")
                .Select(item => item.Element("id")!.Value)
                .ToArray();
            var variantIds = product.Element("variants")!
                .Element("variant")!
                .Element("related_items__beta")!
                .Elements("item")
                .Select(item => item.Element("id")!.Value)
                .ToArray();
            var instruction = relatedItems.Nodes().OfType<XProcessingInstruction>().Single().Data;

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", directIds)).IsEqualTo("110|210");
            await Assert.That(string.Join("|", variantIds)).IsEqualTo("130|430");
            await Assert.That(instruction).IsEqualTo("stay");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_SortsXmlFilesFromDirectoryRecursivelyInPlace()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var nestedDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "nested"));
            var rootPath = Path.Combine(tempDirectory.FullName, "a.xml");
            var nestedPath = Path.Combine(nestedDirectory.FullName, "b.xml");

            await File.WriteAllTextAsync(rootPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");
            await File.WriteAllTextAsync(nestedPath, "<Catalog><Books><Book id=\"4\" /><Book id=\"3\" /></Books></Catalog>");

            var exitCode = CreateApplication().Run([tempDirectory.FullName, "--sort", "/Catalog/Books/Book:@id", "--in-place"]);

            var rootOutput = XDocument.Parse(await File.ReadAllTextAsync(rootPath));
            var nestedOutput = XDocument.Parse(await File.ReadAllTextAsync(nestedPath));

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", rootOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("1|2");
            await Assert.That(string.Join("|", nestedOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("3|4");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_SortsMatchingGlobInputsInPlace()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var firstPath = Path.Combine(tempDirectory.FullName, "a.xml");
            var secondPath = Path.Combine(tempDirectory.FullName, "b.xml");

            await File.WriteAllTextAsync(firstPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");
            await File.WriteAllTextAsync(secondPath, "<Catalog><Books><Book id=\"4\" /><Book id=\"3\" /></Books></Catalog>");

            int exitCode;

            lock (WorkingDirectoryLock)
            {
                using var currentDirectoryScope = new CurrentDirectoryScope(tempDirectory.FullName);
                exitCode = CreateApplication().Run(["*.xml", "--sort", "/Catalog/Books/Book:@id", "--in-place"]);
            }

            var firstOutput = XDocument.Parse(await File.ReadAllTextAsync(firstPath));
            var secondOutput = XDocument.Parse(await File.ReadAllTextAsync(secondPath));

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", firstOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("1|2");
            await Assert.That(string.Join("|", secondOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("3|4");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_WritesDirectoryInputToDifferentOutputDirectoryPreservingStructure()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "input"));
            var nestedInputDirectory = Directory.CreateDirectory(Path.Combine(inputDirectory.FullName, "nested"));
            var outputDirectory = Path.Combine(tempDirectory.FullName, "output");
            var rootInputPath = Path.Combine(inputDirectory.FullName, "a.xml");
            var nestedInputPath = Path.Combine(nestedInputDirectory.FullName, "b.xml");
            var rootOutputPath = Path.Combine(outputDirectory, "a.sorted.xml");
            var nestedOutputPath = Path.Combine(outputDirectory, "nested", "b.sorted.xml");

            await File.WriteAllTextAsync(rootInputPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");
            await File.WriteAllTextAsync(nestedInputPath, "<Catalog><Books><Book id=\"4\" /><Book id=\"3\" /></Books></Catalog>");

            var exitCode = CreateApplication().Run([inputDirectory.FullName, "--sort", "/Catalog/Books/Book:@id", "--write-new", "--output-dir", outputDirectory]);

            var rootOutput = XDocument.Parse(await File.ReadAllTextAsync(rootOutputPath));
            var nestedOutput = XDocument.Parse(await File.ReadAllTextAsync(nestedOutputPath));
            var rootInput = XDocument.Parse(await File.ReadAllTextAsync(rootInputPath));

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(File.Exists(rootOutputPath)).IsTrue();
            await Assert.That(File.Exists(nestedOutputPath)).IsTrue();
            await Assert.That(string.Join("|", rootOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("1|2");
            await Assert.That(string.Join("|", nestedOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("3|4");
            await Assert.That(string.Join("|", rootInput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("2|1");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_WritesSortedCopiesForMatchingGlobInputs()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "books.xml");
            var outputPath = Path.Combine(tempDirectory.FullName, "books.ordered.xml");

            await File.WriteAllTextAsync(inputPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");

            int exitCode;

            lock (WorkingDirectoryLock)
            {
                using var currentDirectoryScope = new CurrentDirectoryScope(tempDirectory.FullName);
                exitCode = CreateApplication().Run(["*.xml", "--sort", "/Catalog/Books/Book:@id", "--write-new", "--suffix", ".ordered"]);
            }

            var original = XDocument.Parse(await File.ReadAllTextAsync(inputPath));
            var writtenCopy = XDocument.Parse(await File.ReadAllTextAsync(outputPath));

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", original.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("2|1");
            await Assert.That(string.Join("|", writtenCopy.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("1|2");
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Run_RenamesSortedGlobInputsWithSuffix()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "books.xml");
            var renamedPath = Path.Combine(tempDirectory.FullName, "books.sorted.xml");

            await File.WriteAllTextAsync(inputPath, "<Catalog><Books><Book id=\"2\" /><Book id=\"1\" /></Books></Catalog>");

            int exitCode;

            lock (WorkingDirectoryLock)
            {
                using var currentDirectoryScope = new CurrentDirectoryScope(tempDirectory.FullName);
                exitCode = CreateApplication().Run(["*.xml", "--sort", "/Catalog/Books/Book:@id", "--rename"]);
            }

            var renamedOutput = XDocument.Parse(await File.ReadAllTextAsync(renamedPath));

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(File.Exists(inputPath)).IsFalse();
            await Assert.That(File.Exists(renamedPath)).IsTrue();
            await Assert.That(string.Join("|", renamedOutput.Root!.Element("Books")!.Elements("Book").Select(book => book.Attribute("id")!.Value))).IsEqualTo("1|2");
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

            exitCode = CreateApplication().Run(["--sort", "/Catalog/Books/Book:@id"]);
            stderr = consoleScope.StandardError;
        }

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(stderr.Contains("unexpected end", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task Run_SortsByTagNameWhenSortTagsFlagIsSupplied()
    {
        const string input = "<Root><Zebra /><Apple /><Mango /></Root>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = CreateApplication().Run(["--sort-tags"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var document = XDocument.Parse(stdout);
        var names = document.Root!.Elements().Select(e => e.Name.LocalName).ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", names)).IsEqualTo("Apple|Mango|Zebra");
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Run_SortsByTagNameFromConfigurationDefault()
    {
        const string input = "<Root><Zebra /><Apple /><Mango /></Root>";

        int exitCode;
        string stdout;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = new XmlSortApplication(new FakeUserConfigurationLoader(new UserConfiguration(
                [],
                FormatXml: false,
                FormatJson: false,
                SortByTagName: true))).Run([]);
            stdout = consoleScope.StandardOutput;
        }

        var document = XDocument.Parse(stdout);
        var names = document.Root!.Elements().Select(e => e.Name.LocalName).ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", names)).IsEqualTo("Apple|Mango|Zebra");
    }

    [Test]
    public async Task Run_PromotesElementSortKeysWhenSortTagsFlagIsCombinedWithSortRule()
    {
        const string input = "<Catalog><Books><Book><Meta>B</Meta><Title>Beta</Title><Author>Baker</Author></Book></Books></Catalog>";

        int exitCode;
        string stdout;
        string stderr;

        lock (ConsoleLock)
        {
            using var consoleScope = new ConsoleScope(input);

            exitCode = CreateApplication().Run(["--sort", "/Catalog/Books/Book:Title", "--sort-tags"]);
            stdout = consoleScope.StandardOutput;
            stderr = consoleScope.StandardError;
        }

        var childNames = XDocument.Parse(stdout)
            .Root!
            .Element("Books")!
            .Element("Book")!
            .Elements()
            .Select(element => element.Name.LocalName)
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", childNames)).IsEqualTo("Title|Author|Meta");
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    private static XmlSortApplication CreateApplication()
    {
        return new XmlSortApplication(new FakeUserConfigurationLoader(configuration: null));
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

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _originalPath;

        public CurrentDirectoryScope(string path)
        {
            _originalPath = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(path);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalPath);
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
