using System.Xml;
using System.Xml.Linq;

internal static class CanonicalXmlFormatter
{
    public static void Write(XDocument document, TextWriter writer)
    {
        var normalizedDocument = new XDocument(document);
        NormalizeWhitespace(normalizedDocument.Root);

        using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
        {
            Indent = true,
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        });

        normalizedDocument.Save(xmlWriter);
    }

    private static void NormalizeWhitespace(XElement? element)
    {
        if (element is null)
        {
            return;
        }

        if (element.HasElements)
        {
            element.Nodes()
                .OfType<XText>()
                .Where(text => string.IsNullOrWhiteSpace(text.Value))
                .Remove();
        }

        foreach (var child in element.Elements())
        {
            NormalizeWhitespace(child);
        }
    }
}
