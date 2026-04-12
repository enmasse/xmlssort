using System.Xml.Linq;

namespace xmlssort.Tests;

public class EmbeddedJsonFormatterTests
{
    [Test]
    public async Task Apply_FormatsValidJsonInLeafElementValues()
    {
        var document = XDocument.Parse(
            """
            <Root>
              <Payload>{"name":"Alice","roles":["admin","user"]}</Payload>
            </Root>
            """);

        EmbeddedJsonFormatter.Apply(document);

        var value = document.Root!.Element("Payload")!.Value;

        await Assert.That(value.Contains(Environment.NewLine, StringComparison.Ordinal)).IsTrue();
        await Assert.That(value.Contains("\"name\": \"Alice\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(value.Contains("\"roles\"", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Apply_FormatsValidJsonInLeafElementValuesAfterLeadingWhitespace()
    {
        var document = XDocument.Parse(
            """
            <Root>
              <Payload>  {"name":"Alice","roles":["admin","user"]}</Payload>
            </Root>
            """);

        EmbeddedJsonFormatter.Apply(document);

        var value = document.Root!.Element("Payload")!.Value;

        await Assert.That(value.Contains(Environment.NewLine, StringComparison.Ordinal)).IsTrue();
        await Assert.That(value.Contains("\"name\": \"Alice\"", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Apply_LeavesInvalidJsonUnchanged()
    {
        const string invalidJson = "{not-json}";
        var document = XDocument.Parse($"<Root><Payload>{invalidJson}</Payload></Root>");

        EmbeddedJsonFormatter.Apply(document);

        await Assert.That(document.Root!.Element("Payload")!.Value).IsEqualTo(invalidJson);
    }

    [Test]
    public async Task Apply_PreservesVisibleUtf8CharactersWhenFormattingJson()
    {
        var document = XDocument.Parse(
            """
            <Root>
              <Payload>{"text":"Värmepump åäö"}</Payload>
            </Root>
            """);

        EmbeddedJsonFormatter.Apply(document);

        var value = document.Root!.Element("Payload")!.Value;

        await Assert.That(value.Contains("Värmepump åäö", StringComparison.Ordinal)).IsTrue();
        await Assert.That(value.Contains("\\u00", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Apply_PreservesCDataWhenFormattingJson()
    {
        var document = XDocument.Parse("<Root><Payload><![CDATA[{\"name\":\"Alice\"}]]></Payload></Root>");

        EmbeddedJsonFormatter.Apply(document);

        var payload = document.Root!.Element("Payload")!;

        await Assert.That(payload.Nodes().Single() is XCData).IsTrue();
        await Assert.That(payload.Value.Contains("\"name\": \"Alice\"", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Apply_SkipsElementsWithChildElements()
    {
        var document = XDocument.Parse(
            """
            <Root>
              <Payload>
                {"name":"Alice"}
                <Child />
              </Payload>
            </Root>
            """);

        var original = document.Root!.Element("Payload")!.Value;

        EmbeddedJsonFormatter.Apply(document);

        await Assert.That(document.Root!.Element("Payload")!.Value).IsEqualTo(original);
    }

    [Test]
    public async Task Apply_SkipsLeafValuesThatDoNotStartWithJsonContainer()
    {
        var document = XDocument.Parse(
            """
            <Root>
              <Boolean>true</Boolean>
              <Number>123</Number>
              <String>&quot;Alice&quot;</String>
            </Root>
            """);

        EmbeddedJsonFormatter.Apply(document);

        await Assert.That(document.Root!.Element("Boolean")!.Value).IsEqualTo("true");
        await Assert.That(document.Root!.Element("Number")!.Value).IsEqualTo("123");
        await Assert.That(document.Root!.Element("String")!.Value).IsEqualTo("\"Alice\"");
    }
}
