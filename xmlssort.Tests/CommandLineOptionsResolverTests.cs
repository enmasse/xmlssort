namespace xmlssort.Tests;

public class CommandLineOptionsResolverTests
{
    [Test]
    public async Task Resolve_UsesConfigurationDefaultsWhenCommandLineDoesNotProvideOperations()
    {
        var commandLineOptions = CommandLineOptions.Parse(["input.xml"]);
        var configuration = new UserConfiguration([SortRule.Parse("/Catalog/Books/Book:@id")], FormatXml: true, FormatJson: true);

        var resolved = CommandLineOptionsResolver.Resolve(commandLineOptions, configuration);

        await Assert.That(resolved.SortRules.Count).IsEqualTo(1);
        await Assert.That(resolved.SortRules[0].TargetElementName).IsEqualTo("Book");
        await Assert.That(resolved.FormatXml).IsTrue();
        await Assert.That(resolved.FormatJson).IsTrue();
    }

    [Test]
    public async Task Resolve_PrefersCommandLineSortRulesOverConfigurationDefaults()
    {
        var commandLineOptions = CommandLineOptions.Parse(["input.xml", "--sort", "/Catalog/Books/Book:Title"]);
        var configuration = new UserConfiguration([SortRule.Parse("/Catalog/Books/Book:@id")], FormatXml: false, FormatJson: false);

        var resolved = CommandLineOptionsResolver.Resolve(commandLineOptions, configuration);

        await Assert.That(resolved.SortRules.Count).IsEqualTo(1);
        await Assert.That(resolved.SortRules[0].Keys[0].Name).IsEqualTo("Title");
    }

    [Test]
    public async Task Resolve_UsesConfigurationFormatXmlWhenCommandLineOmitsIt()
    {
        var commandLineOptions = CommandLineOptions.Parse(["input.xml", "--sort", "/Catalog/Books/Book:@id"]);
        var configuration = new UserConfiguration([], FormatXml: true, FormatJson: false);

        var resolved = CommandLineOptionsResolver.Resolve(commandLineOptions, configuration);

        await Assert.That(resolved.FormatXml).IsTrue();
    }

    [Test]
    public async Task Resolve_AllowsFormatXmlAsTheOnlyOperation()
    {
        var resolved = CommandLineOptionsResolver.Resolve(
            CommandLineOptions.Parse(["input.xml", "--format-xml"]),
            userConfiguration: null);

        await Assert.That(resolved.FormatXml).IsTrue();
        await Assert.That(resolved.SortRules.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_ThrowsWhenNeitherCommandLineNorConfigurationProvidesOperations()
    {
        var threw = false;
        var message = string.Empty;

        try
        {
            CommandLineOptionsResolver.Resolve(CommandLineOptions.Parse(["input.xml"]), userConfiguration: null);
        }
        catch (ArgumentException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message).IsEqualTo("At least one operation is required. Supply --sort, --format-xml, and/or --format-json.");
    }
}
