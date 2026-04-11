using System.Xml.Linq;

namespace xmlssort.Tests;

public class XmlSorterTests
{
    [Test]
    public async Task Apply_SortsMatchingSiblingsRecursivelyByDefault()
    {
        var document = XDocument.Parse(
            """
            <Library>
              <Sections>
                <Section>
                  <Name>B</Name>
                  <Sections>
                    <Section><Name>D</Name></Section>
                    <Section><Name>C</Name></Section>
                  </Sections>
                </Section>
                <Section>
                  <Name>A</Name>
                  <Sections>
                    <Section><Name>F</Name></Section>
                    <Section><Name>E</Name></Section>
                  </Sections>
                </Section>
              </Sections>
            </Library>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Library/Sections/Section:Name")]);

        var topLevelSections = document.Root!
            .Element("Sections")!
            .Elements("Section")
            .ToArray();

        var topLevelNames = topLevelSections
            .Select(section => section.Element("Name")!.Value)
            .ToArray();

        var nestedNames = topLevelSections
            .Select(section => string.Join(",", section.Element("Sections")!.Elements("Section").Select(child => child.Element("Name")!.Value)))
            .ToArray();

        await Assert.That(string.Join("|", topLevelNames)).IsEqualTo("A|B");
        await Assert.That(string.Join("|", nestedNames)).IsEqualTo("E,F|C,D");
    }

    [Test]
    public async Task Apply_SortsByAttributeThenElementValue()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book id="2"><Title>Beta</Title></Book>
                <Book id="1"><Title>Zulu</Title></Book>
                <Book id="1"><Title>Alpha</Title></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@id,Title")]);

        var titles = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => $"{book.Attribute("id")!.Value}:{book.Element("Title")!.Value}")
            .ToArray();

        await Assert.That(string.Join("|", titles)).IsEqualTo("1:Alpha|1:Zulu|2:Beta");
    }

    [Test]
    public async Task Apply_SortsInDescendingOrder()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book><Title>Alpha</Title></Book>
                <Book><Title>Zulu</Title></Book>
                <Book><Title>Beta</Title></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:Title desc")]);

        var titles = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Element("Title")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", titles)).IsEqualTo("Zulu|Beta|Alpha");
    }

    [Test]
    public async Task Apply_PreservesCommentsWhileSortingSiblings()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <!-- keep -->
                <Book id="2" />
                <Book id="1" />
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@id")]);

        var books = document.Root!.Element("Books")!;
        var ids = books.Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();
        var comment = books.Nodes().OfType<XComment>().Single().Value;

        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
        await Assert.That(comment).IsEqualTo(" keep ");
    }

    [Test]
    public async Task Apply_PreservesNonTargetContentWhileReorderingMatchingElements()
    {
        var document = XDocument.Parse(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <?catalog keep?>
            <Catalog version="1">
              <Header><![CDATA[{"message":"keep"}]]></Header>
              <Books>
                <!-- keep -->
                <Book id="2"><Title>Zebra</Title><Meta foo="b">Beta</Meta></Book>
                <?between stay?>
                <Book id="1"><Title>Alpha</Title><Meta foo="a">Alpha</Meta></Book>
                <Note importance="high">unchanged</Note>
              </Books>
              <Footer>tail</Footer>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@id")]);

        var expected = XDocument.Parse(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <?catalog keep?>
            <Catalog version="1">
              <Header><![CDATA[{"message":"keep"}]]></Header>
              <Books>
                <!-- keep -->
                <Book id="1"><Title>Alpha</Title><Meta foo="a">Alpha</Meta></Book>
                <?between stay?>
                <Book id="2"><Title>Zebra</Title><Meta foo="b">Beta</Meta></Book>
                <Note importance="high">unchanged</Note>
              </Books>
              <Footer>tail</Footer>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        await Assert.That(XNode.DeepEquals(document, expected)).IsTrue();
    }

    [Test]
    public async Task Apply_ThrowsForRootMismatch()
    {
        var document = XDocument.Parse("<Library><Sections /></Library>");
        var threw = false;
        var message = string.Empty;

        try
        {
            XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@id")]);
        }
        catch (InvalidOperationException ex)
        {
            threw = true;
            message = ex.Message;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(message.Contains("does not match the document root", StringComparison.Ordinal)).IsTrue();
    }
}
