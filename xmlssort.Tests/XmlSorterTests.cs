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
    public async Task Apply_SortsDifferentLevelsUsingDifferentRules()
    {
        var document = XDocument.Parse(
            """
            <Library>
              <Sections>
                <Section>
                  <Name>Fiction</Name>
                  <Books>
                    <Book id="2"><Title>Zebra</Title></Book>
                    <Book id="1"><Title>Alpha</Title></Book>
                  </Books>
                </Section>
                <Section>
                  <Name>Art</Name>
                  <Books>
                    <Book id="4"><Title>Modern</Title></Book>
                    <Book id="3"><Title>Classic</Title></Book>
                  </Books>
                </Section>
              </Sections>
            </Library>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document,
        [
            SortRule.Parse("/Library/Sections/Section:Name"),
            SortRule.Parse("/Library/Sections/Section/Books/Book:@id")
        ]);

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

        await Assert.That(string.Join("|", sectionNames)).IsEqualTo("Art|Fiction");
        await Assert.That(string.Join("|", bookIds)).IsEqualTo("3,4|1,2");
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
    public async Task Apply_SortsNumericElementValuesNumerically()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book><Order>10</Order></Book>
                <Book><Order>2</Order></Book>
                <Book><Order>100</Order></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:Order numeric")]);

        var orders = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Element("Order")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", orders)).IsEqualTo("2|10|100");
    }

    [Test]
    public async Task Apply_SortsNumericAttributeValuesNumerically()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book rank="10"><Title>Gamma</Title></Book>
                <Book rank="2"><Title>Beta</Title></Book>
                <Book rank="100"><Title>Alpha</Title></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@rank numeric")]);

        var ranks = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("rank")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", ranks)).IsEqualTo("2|10|100");
    }

    [Test]
    public async Task Apply_UsesNumericKeyBeforeStringTieBreaker()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book><Order>10</Order><Title>Zulu</Title></Book>
                <Book><Order>2</Order><Title>Zulu</Title></Book>
                <Book><Order>10</Order><Title>Alpha</Title></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:Order numeric,Title")]);

        var values = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => $"{book.Element("Order")!.Value}:{book.Element("Title")!.Value}")
            .ToArray();

        await Assert.That(string.Join("|", values)).IsEqualTo("2:Zulu|10:Alpha|10:Zulu");
    }

    [Test]
    public async Task Apply_SortsNumericElementValuesInDescendingOrder()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book><Order>10</Order></Book>
                <Book><Order>2</Order></Book>
                <Book><Order>100</Order></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:Order numeric desc")]);

        var orders = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Element("Order")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", orders)).IsEqualTo("100|10|2");
    }

    [Test]
    public async Task Apply_PlacesNumericValuesBeforeNonNumericValuesWhenSortingNumerically()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book><Order>unknown</Order></Book>
                <Book><Order>10</Order></Book>
                <Book><Order>2</Order></Book>
                <Book><Order>n/a</Order></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:Order numeric")]);

        var orders = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Element("Order")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", orders)).IsEqualTo("2|10|n/a|unknown");
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
    public async Task Apply_SortsNestedWildcardContainersByIdWithinEachProduct()
    {
        var document = XDocument.Parse(
            """
            <operations>
              <add>
                <product>
                  <product_key>pk-2</product_key>
                  <related_items__alpha>
                    <item><id>210</id></item>
                    <item><id>110</id></item>
                  </related_items__alpha>
                  <related_items__beta>
                    <item><id>420</id></item>
                    <item><id>410</id></item>
                  </related_items__beta>
                </product>
                <product>
                  <product_key>pk-1</product_key>
                  <related_items__alpha>
                    <item><id>320</id></item>
                    <item><id>120</id></item>
                  </related_items__alpha>
                </product>
              </add>
            </operations>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document,
        [
            SortRule.Parse("/operations/add/product/related_items__alpha/item:id"),
            SortRule.Parse("/operations/add/product/related_items__beta/item:id")
        ]);

        var products = document.Root!
            .Element("add")!
            .Elements("product")
            .ToArray();

        var productKeys = products
            .Select(product => product.Element("product_key")!.Value)
            .ToArray();

        var alphaIds = products
            .Select(product => string.Join(",", product.Element("related_items__alpha")!.Elements("item").Select(item => item.Element("id")!.Value)))
            .ToArray();

        var betaIds = products
            .Select(product => product.Element("related_items__beta") is null
                ? string.Empty
                : string.Join(",", product.Element("related_items__beta")!.Elements("item").Select(item => item.Element("id")!.Value)))
            .ToArray();

        await Assert.That(string.Join("|", productKeys)).IsEqualTo("pk-2|pk-1");
        await Assert.That(string.Join("|", alphaIds)).IsEqualTo("110,210|120,320");
        await Assert.That(string.Join("|", betaIds)).IsEqualTo("410,420|");
    }

    [Test]
    public async Task Apply_SortsWildcardContainersByIdWithinEachVariant()
    {
        var document = XDocument.Parse(
            """
            <operations>
              <add>
                <product>
                  <product_key>pk-1</product_key>
                  <variants>
                    <variant>
                      <id>v-2</id>
                      <related_items__alpha>
                        <item><id>320</id></item>
                        <item><id>120</id></item>
                        <item><id>220</id></item>
                      </related_items__alpha>
                    </variant>
                    <variant>
                      <id>v-1</id>
                      <related_items__beta>
                        <item><id>430</id></item>
                        <item><id>130</id></item>
                        <item><id>230</id></item>
                      </related_items__beta>
                    </variant>
                  </variants>
                </product>
              </add>
            </operations>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document,
        [
            SortRule.Parse("/operations/add/product/variants/variant/related_items__alpha/item:id"),
            SortRule.Parse("/operations/add/product/variants/variant/related_items__beta/item:id")
        ]);

        var variants = document.Root!
            .Element("add")!
            .Element("product")!
            .Element("variants")!
            .Elements("variant")
            .ToArray();

        var alphaIds = string.Join(",", variants[0].Element("related_items__alpha")!.Elements("item").Select(item => item.Element("id")!.Value));
        var betaIds = string.Join(",", variants[1].Element("related_items__beta")!.Elements("item").Select(item => item.Element("id")!.Value));

        await Assert.That(alphaIds).IsEqualTo("120,220,320");
        await Assert.That(betaIds).IsEqualTo("130,230,430");
    }

    [Test]
    public async Task Apply_SortsWildcardContainersByIdWithSingleWildcardRule()
    {
        var document = XDocument.Parse(
            """
            <operations>
              <add>
                <product>
                  <product_key>pk-1</product_key>
                  <related_items__alpha>
                    <item><id>210</id></item>
                    <item><id>110</id></item>
                  </related_items__alpha>
                  <variants>
                    <variant>
                      <id>v-1</id>
                      <related_items__beta>
                        <item><id>430</id></item>
                        <item><id>130</id></item>
                        <item><id>230</id></item>
                      </related_items__beta>
                    </variant>
                  </variants>
                </product>
              </add>
            </operations>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document, [SortRule.Parse("/operations/add/product/**/related_items__*/item:id")]);

        var product = document.Root!
            .Element("add")!
            .Element("product")!;

        var productItems = string.Join(",", product.Element("related_items__alpha")!.Elements("item").Select(item => item.Element("id")!.Value));
        var variantItems = string.Join(",", product.Element("variants")!.Element("variant")!.Element("related_items__beta")!.Elements("item").Select(item => item.Element("id")!.Value));

        await Assert.That(productItems).IsEqualTo("110,210");
        await Assert.That(variantItems).IsEqualTo("130,230,430");
    }

    [Test]
    public async Task Apply_IgnoresNonMatchingRulesWhenAnotherRuleApplies()
    {
        var document = XDocument.Parse(
            """
            <Catalog>
              <Books>
                <Book id="2"><Title>Beta</Title></Book>
                <Book id="1"><Title>Alpha</Title></Book>
              </Books>
            </Catalog>
            """,
            LoadOptions.PreserveWhitespace);

        XmlSorter.Apply(document,
        [
            SortRule.Parse("/Library/Sections/Section:Name"),
            SortRule.Parse("/Catalog/Books/Book:@id")
        ]);

        var ids = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();

        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
    }

    [Test]
    public async Task Apply_IgnoresRootMismatchWhenNoRulesApply()
    {
        var document = XDocument.Parse("<Library><Sections /></Library>");

        XmlSorter.Apply(document, [SortRule.Parse("/Catalog/Books/Book:@id")]);

        await Assert.That(document.Root!.Name.LocalName).IsEqualTo("Library");
    }
}
