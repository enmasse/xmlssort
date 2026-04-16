namespace xmlssort.Tests;

public class XmlDiffServiceTests
{
    [Test]
    public async Task LoadDefaults_ReadsSharedConfigurationFile()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configurationPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(configurationPath, """
                {
                  "sort": [
                    "/Catalog/Books/Book:@id"
                  ],
                  "formatJson": true,
                  "formatXml": true,
                  "sortTags": true
                }
                """);

            var service = new XmlDiffService();
            var defaults = service.LoadDefaults(configurationPath);

            await Assert.That(defaults.SortRules.Count).IsEqualTo(1);
            await Assert.That(defaults.SortRules[0]).IsEqualTo("/Catalog/Books/Book:@id");
            await Assert.That(defaults.FormatXml).IsTrue();
            await Assert.That(defaults.FormatJson).IsTrue();
            await Assert.That(defaults.SortByTagName).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Generate_UsesConfiguredDefaultsWhenRequestOmitsThem()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            var configurationPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\"/><Book id=\"2\"/></Books></Catalog>");
            await File.WriteAllTextAsync(configurationPath, """
                {
                  "sort": [
                    "/Catalog/Books/Book:@id"
                  ],
                  "formatXml": true
                }
                """);

            var service = new XmlDiffService();
            var result = service.Generate(new XmlDiffRequest(leftPath, rightPath, [], false, false, false), configurationPath);

            await Assert.That(result.EffectiveOptions.SortRules.Count).IsEqualTo(1);
            await Assert.That(result.EffectiveOptions.SortRules[0]).IsEqualTo("/Catalog/Books/Book:@id");
            await Assert.That(result.EffectiveOptions.FormatXml).IsTrue();
            await Assert.That(result.TextReport.Contains("No differences found.", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Generate_ReturnsHtmlAndTextReportsForChangedElements()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"1\"><Title>Alpha</Title></Book></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\"><Title>Beta</Title></Book></Books></Catalog>");

            var service = new XmlDiffService();
            var result = service.Generate(new XmlDiffRequest(leftPath, rightPath, ["/Catalog/Books/Book:@id"], false, false, false), string.Empty);

            await Assert.That(result.TextReport.Contains("Changed: 1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(result.TextReport.Contains("Title: left='Alpha', right='Beta'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(result.HtmlReport.Contains("<html", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(result.HtmlReport.Contains("@id=1", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}
