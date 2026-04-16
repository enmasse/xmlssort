using System.Xml.Linq;

internal static class XmlDocumentProcessor
{
    public static XDocument Normalize(XDocument document, IReadOnlyList<SortRule> sortRules, bool sortByTagName, bool formatJson)
    {
        XmlSorter.Apply(document, sortRules, sortByTagName);

        if (formatJson)
        {
            EmbeddedJsonFormatter.Apply(document);
        }

        return document;
    }
}
