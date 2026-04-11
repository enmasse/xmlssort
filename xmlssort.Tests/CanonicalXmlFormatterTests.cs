using System.Xml.Linq;

namespace xmlssort.Tests;

public class CanonicalXmlFormatterTests
{
    [Test]
    public async Task Write_NormalizesIndentationWhitespaceAndWritesIndentedXml()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book id="2" />
                <Book id="1" />
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        using var writer = new StringWriter();

        CanonicalXmlFormatter.Write(document, writer);

        var output = writer.ToString();

        await Assert.That(output.Contains(Environment.NewLine + "  <Books>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(output.Contains(Environment.NewLine + "    <Book id=\"2\" />", StringComparison.Ordinal)).IsTrue();
        await Assert.That(output.Contains(Environment.NewLine + "    <Book id=\"1\" />", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Write_PreservesMeaningfulLeafTextWhitespace()
    {
        var document = XDocument.Parse("<Root><Value>  keep me  </Value></Root>", LoadOptions.PreserveWhitespace);

        using var writer = new StringWriter();

        CanonicalXmlFormatter.Write(document, writer);

        var reparsed = XDocument.Parse(writer.ToString());

        await Assert.That(reparsed.Root!.Element("Value")!.Value).IsEqualTo("  keep me  ");
    }
}
