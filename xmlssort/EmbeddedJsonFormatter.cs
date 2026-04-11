using System.Text.Json;
using System.Xml.Linq;

internal static class EmbeddedJsonFormatter
{
    public static void Apply(XDocument document)
    {
        if (document.Root is null)
        {
            throw new ArgumentException("The XML document does not have a root element.");
        }

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            FormatElementValue(element);
        }
    }

    private static void FormatElementValue(XElement element)
    {
        if (element.HasElements)
        {
            return;
        }

        var contentNodes = element.Nodes().ToList();

        if (contentNodes.Count == 0)
        {
            return;
        }

        if (contentNodes.Any(node => node is not XText and not XCData))
        {
            return;
        }

        var value = element.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
 
        if (!TryFormatJson(value, out var formattedJson))
        {
            return;
        }

        if (contentNodes.Count == 1 && contentNodes[0] is XCData)
        {
            element.ReplaceNodes(new XCData(formattedJson));
            return;
        }

        element.Value = formattedJson;
    }

    private static bool TryFormatJson(string value, out string formattedJson)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            formattedJson = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return true;
        }
        catch (JsonException)
        {
            formattedJson = string.Empty;
            return false;
        }
    }
}
