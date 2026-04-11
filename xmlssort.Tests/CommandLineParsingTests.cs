namespace xmlssort.Tests;

public class CommandLineParsingTests
{
    [Test]
    public async Task Parse_ReadsInputOutputAndMultipleSortRules()
    {
        var options = CommandLineOptions.Parse([
            "input.xml",
            "--sort", "/Catalog/Books/Book:@id,Title desc",
            "--sort=/Catalog/Books/Book/Chapters/Chapter:@number",
            "--output", "sorted.xml"
        ]);

        await Assert.That(options.InputPath).IsEqualTo("input.xml");
        await Assert.That(options.OutputPath).IsEqualTo("sorted.xml");
        await Assert.That(options.SortRules.Count).IsEqualTo(2);
        await Assert.That(options.SortRules[0].Keys.Count).IsEqualTo(2);
        await Assert.That(options.SortRules[1].PathSegments[^1]).IsEqualTo("Chapter");
    }

    [Test]
    public async Task Parse_SupportsAttributeAndElementKeysWithDirections()
    {
        var rule = SortRule.Parse("/Catalog/Books/Book:@id desc,Title");

        await Assert.That(string.Join("/", rule.PathSegments)).IsEqualTo("Catalog/Books/Book");
        await Assert.That(rule.Keys[0].Name).IsEqualTo("id");
        await Assert.That(rule.Keys[0].Kind).IsEqualTo(SortKeyKind.Attribute);
        await Assert.That(rule.Keys[0].Direction).IsEqualTo(SortDirection.Descending);
        await Assert.That(rule.Keys[1].Name).IsEqualTo("Title");
        await Assert.That(rule.Keys[1].Kind).IsEqualTo(SortKeyKind.Element);
        await Assert.That(rule.Keys[1].Direction).IsEqualTo(SortDirection.Ascending);
    }

    [Test]
    public async Task Parse_ReadsGlobalFormatJsonFlag()
    {
        var options = CommandLineOptions.Parse(["input.xml", "--format-json"]);

        await Assert.That(options.InputPath).IsEqualTo("input.xml");
        await Assert.That(options.FormatXml).IsFalse();
        await Assert.That(options.FormatJson).IsTrue();
        await Assert.That(options.SortRules.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_ReadsGlobalFormatXmlFlag()
    {
        var options = CommandLineOptions.Parse(["input.xml", "--format-xml"]);

        await Assert.That(options.InputPath).IsEqualTo("input.xml");
        await Assert.That(options.FormatXml).IsTrue();
        await Assert.That(options.FormatJson).IsFalse();
        await Assert.That(options.SortRules.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_AllowsNoOperationsSoConfigurationCanSupplyDefaults()
    {
        var options = CommandLineOptions.Parse(["input.xml"]);

        await Assert.That(options.InputPath).IsEqualTo("input.xml");
        await Assert.That(options.SortRules.Count).IsEqualTo(0);
        await Assert.That(options.FormatXml).IsFalse();
        await Assert.That(options.FormatJson).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsMultipleInputsWhenBatchModeIsUsed()
    {
        var options = CommandLineOptions.Parse(["*.xml", "nested\\*.xml", "--sort", "/Catalog/Books/Book:@id", "--write-new"]);

        await Assert.That(options.InputPaths.Count).IsEqualTo(2);
        await Assert.That(options.BatchOutputMode).IsEqualTo(BatchOutputMode.WriteNew);
        await Assert.That(options.Suffix).IsEqualTo(BatchOutputPathResolver.DefaultSuffix);
    }

    [Test]
    public async Task Parse_ReadsExplicitSuffixForRenameMode()
    {
        var options = CommandLineOptions.Parse(["*.xml", "--sort", "/Catalog/Books/Book:@id", "--rename", "--suffix", ".bak"]);

        await Assert.That(options.BatchOutputMode).IsEqualTo(BatchOutputMode.Rename);
        await Assert.That(options.Suffix).IsEqualTo(".bak");
    }

    [Test]
    public async Task Parse_ReadsOutputDirectoryForWriteNewMode()
    {
        var options = CommandLineOptions.Parse(["input", "--sort", "/Catalog/Books/Book:@id", "--write-new", "--output-dir", "sorted"]);

        await Assert.That(options.BatchOutputMode).IsEqualTo(BatchOutputMode.WriteNew);
        await Assert.That(options.OutputDirectory).IsEqualTo("sorted");
    }

    [Test]
    public async Task Parse_ThrowsForMultipleInputsWithoutBatchMode()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            CommandLineOptions.Parse(["a.xml", "b.xml", "--sort", "/Catalog/Books/Book:@id"]);
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("Multiple input files require --in-place, --rename, or --write-new.");
    }

    [Test]
    public async Task Parse_ThrowsForSuffixWithoutRenameOrWriteNew()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            CommandLineOptions.Parse(["input.xml", "--sort", "/Catalog/Books/Book:@id", "--suffix", ".sorted"]);
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("The --suffix option can only be used with --rename or --write-new.");
    }

    [Test]
    public async Task Parse_ThrowsForOutputDirectoryWithoutWriteNew()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            CommandLineOptions.Parse(["input", "--sort", "/Catalog/Books/Book:@id", "--rename", "--output-dir", "sorted"]);
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("The --output-dir option can only be used with --write-new.");
    }

    [Test]
    public async Task Parse_ThrowsForUnknownOption()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            CommandLineOptions.Parse(["--unknown", "value"]);
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("Unknown option '--unknown'.");
    }

    [Test]
    public async Task SortRule_Parse_ThrowsForPathWithoutRootPrefix()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            SortRule.Parse("Catalog/Books/Book:@id");
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("Invalid sort path 'Catalog/Books/Book'. Sort paths must start with '/'.");
    }

    [Test]
    public async Task SortKey_Parse_ThrowsForWhitespaceOnlyKey()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            SortKey.Parse("   ");
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("Sort keys cannot be empty.");
    }
}
