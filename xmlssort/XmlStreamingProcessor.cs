using System.Globalization;
using System.Xml;
using System.Xml.Linq;

internal static class XmlStreamingProcessor
{
    public static bool CanProcess(CommandLineOptions options)
    {
        return !options.FormatJson;
    }

    public static void Process(string? inputPath, string? outputPath, CommandLineOptions options)
    {
        var compiledRules = XmlSorter.CompileRules(options.SortRules);
        var readerSettings = new XmlReaderSettings
        {
            IgnoreWhitespace = options.FormatXml
        };
        var writerSettings = new XmlWriterSettings
        {
            CloseOutput = false,
            Indent = options.FormatXml,
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        if (string.IsNullOrWhiteSpace(inputPath) || inputPath == "-")
        {
            using var reader = XmlReader.Create(Console.In, readerSettings);
            using var writer = XmlWriter.Create(Console.Out, writerSettings);
            Process(reader, writer, compiledRules);
            return;
        }

        using var inputStream = File.OpenRead(inputPath);
        using var readerFromFile = XmlReader.Create(inputStream, readerSettings);

        if (string.IsNullOrWhiteSpace(outputPath) || outputPath == "-")
        {
            using var writer = XmlWriter.Create(Console.Out, writerSettings);
            Process(readerFromFile, writer, compiledRules);
            return;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var outputWriter = new StreamWriter(outputPath);
        using var xmlWriter = XmlWriter.Create(outputWriter, writerSettings);
        Process(readerFromFile, xmlWriter, compiledRules);
    }

    private static void Process(XmlReader reader, XmlWriter writer, IReadOnlyList<CompiledSortRule> rules)
    {
        var currentPath = new List<string>();
        var withinMatchedParents = new bool[rules.Count];

        if (reader.ReadState == ReadState.Initial && !reader.Read())
        {
            return;
        }

        while (!reader.EOF)
        {
            ProcessNode(reader, writer, currentPath, withinMatchedParents, rules);
        }

        writer.Flush();
    }

    private static void ProcessNode(
        XmlReader reader,
        XmlWriter writer,
        List<string> currentPath,
        bool[] withinMatchedParents,
        IReadOnlyList<CompiledSortRule> rules)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                ProcessElement(reader, writer, currentPath, withinMatchedParents, rules);
                break;
            case XmlNodeType.CDATA:
                writer.WriteCData(reader.Value);
                reader.Read();
                break;
            case XmlNodeType.Comment:
                writer.WriteComment(reader.Value);
                reader.Read();
                break;
            case XmlNodeType.DocumentType:
                writer.WriteDocType(reader.Name, null, null, reader.Value);
                reader.Read();
                break;
            case XmlNodeType.ProcessingInstruction:
                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                reader.Read();
                break;
            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.Whitespace:
                writer.WriteWhitespace(reader.Value);
                reader.Read();
                break;
            case XmlNodeType.Text:
                writer.WriteString(reader.Value);
                reader.Read();
                break;
            case XmlNodeType.XmlDeclaration:
                reader.Read();
                break;
            default:
                reader.Read();
                break;
        }
    }

    private static void ProcessElement(
        XmlReader reader,
        XmlWriter writer,
        List<string> currentPath,
        bool[] withinMatchedParents,
        IReadOnlyList<CompiledSortRule> rules)
    {
        var localName = reader.LocalName;
        var namespaceUri = reader.NamespaceURI;
        var isEmptyElement = reader.IsEmptyElement;

        currentPath.Add(localName);

        var childMatchedParents = (bool[])withinMatchedParents.Clone();
        var applicableRules = new List<CompiledSortRule>();

        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var isMatchedParent = rules[ruleIndex].ParentPathMatcher.IsMatch(currentPath);

            if (isMatchedParent && !withinMatchedParents[ruleIndex])
            {
                applicableRules.Add(rules[ruleIndex]);
            }

            childMatchedParents[ruleIndex] = withinMatchedParents[ruleIndex] || isMatchedParent;
        }

        if (applicableRules.Count > 0)
        {
            ProcessMatchedParent(reader, writer, currentPath, rules, applicableRules);
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        writer.WriteStartElement(reader.Prefix, localName, namespaceUri);

        if (reader.HasAttributes)
        {
            writer.WriteAttributes(reader, true);
            reader.MoveToElement();
        }

        if (isEmptyElement)
        {
            writer.WriteEndElement();
            reader.Read();
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        reader.Read();

        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.EndElement
                && string.Equals(reader.LocalName, localName, StringComparison.Ordinal)
                && string.Equals(reader.NamespaceURI, namespaceUri, StringComparison.Ordinal))
            {
                writer.WriteFullEndElement();
                reader.Read();
                break;
            }

            ProcessNode(reader, writer, currentPath, childMatchedParents, rules);
        }

        currentPath.RemoveAt(currentPath.Count - 1);
    }

    private static void ProcessMatchedParent(
        XmlReader reader,
        XmlWriter writer,
        List<string> currentPath,
        IReadOnlyList<CompiledSortRule> rules,
        IReadOnlyList<CompiledSortRule> applicableRules)
    {
        if (applicableRules.Count != 1)
        {
            var element = (XElement)XNode.ReadFrom(reader);
            XmlSorter.Apply(element, rules, currentPath.GetRange(0, currentPath.Count - 1));
            element.WriteTo(writer);
            return;
        }

        var rule = applicableRules[0];
        var localName = reader.LocalName;
        var namespaceUri = reader.NamespaceURI;
        var isEmptyElement = reader.IsEmptyElement;

        writer.WriteStartElement(reader.Prefix, localName, namespaceUri);

        if (reader.HasAttributes)
        {
            writer.WriteAttributes(reader, true);
            reader.MoveToElement();
        }

        if (isEmptyElement)
        {
            writer.WriteEndElement();
            reader.Read();
            return;
        }

        reader.Read();

        var bufferedContent = new BufferedParentContent(rule.Keys);
        var allowRawFragments = writer.Settings?.Indent != true;

        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.EndElement
                && string.Equals(reader.LocalName, localName, StringComparison.Ordinal)
                && string.Equals(reader.NamespaceURI, namespaceUri, StringComparison.Ordinal))
            {
                bufferedContent.WriteTo(writer);
                writer.WriteFullEndElement();
                reader.Read();
                break;
            }

            BufferDirectChild(reader, currentPath, rules, rule, bufferedContent, allowRawFragments);
        }
    }

    private static void BufferDirectChild(
        XmlReader reader,
        IReadOnlyList<string> currentPath,
        IReadOnlyList<CompiledSortRule> rules,
        CompiledSortRule rule,
        BufferedParentContent bufferedContent,
        bool allowRawFragments)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
            {
                if (allowRawFragments && TryBufferRawElement(reader, currentPath, rules, rule, bufferedContent))
                {
                    return;
                }

                var element = (XElement)XNode.ReadFrom(reader);
                XmlSorter.Apply(element, rules, currentPath);

                if (rule.TargetMatcher.IsMatch(element.Name.LocalName))
                {
                    bufferedContent.AddTarget(element, rule.Keys);
                    return;
                }

                bufferedContent.AddFixedElement(element);
                return;
            }
            case XmlNodeType.CDATA:
                bufferedContent.AddFixedNode(new XCData(reader.Value));
                reader.Read();
                return;
            case XmlNodeType.Comment:
                bufferedContent.AddFixedNode(new XComment(reader.Value));
                reader.Read();
                return;
            case XmlNodeType.ProcessingInstruction:
                bufferedContent.AddFixedNode(new XProcessingInstruction(reader.Name, reader.Value));
                reader.Read();
                return;
            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.Text:
            case XmlNodeType.Whitespace:
                bufferedContent.AddFixedNode(new XText(reader.Value));
                reader.Read();
                return;
            default:
                reader.Read();
                return;
        }
    }

    private static bool TryBufferRawElement(
        XmlReader reader,
        IReadOnlyList<string> currentPath,
        IReadOnlyList<CompiledSortRule> rules,
        CompiledSortRule rule,
        BufferedParentContent bufferedContent)
    {
        var localName = reader.LocalName;
        var childPath = new string[currentPath.Count + 1];

        for (var index = 0; index < currentPath.Count; index++)
        {
            childPath[index] = currentPath[index];
        }

        childPath[^1] = localName;

        if (RequiresMaterialization(childPath, rules))
        {
            return false;
        }

        var isTarget = rule.TargetMatcher.IsMatch(localName);

        if (isTarget && !HasAttributeOnlyKeys(rule.Keys))
        {
            return false;
        }

        Dictionary<string, string>? attributeValues = null;

        if (isTarget)
        {
            attributeValues = GetAttributeValues(reader, rule.Keys);
        }

        var rawXml = reader.ReadOuterXml();

        if (isTarget)
        {
            bufferedContent.AddRawTarget(rawXml, CreateSortValues(attributeValues!, rule.Keys));
            return true;
        }

        bufferedContent.AddRawElement(rawXml);
        return true;
    }

    private static bool HasAttributeOnlyKeys(IReadOnlyList<SortKey> keys)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (keys[index].Kind != SortKeyKind.Attribute)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> GetAttributeValues(XmlReader reader, IReadOnlyList<SortKey> keys)
    {
        var values = new Dictionary<string, string>(keys.Count, StringComparer.Ordinal);

        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];

            if (!values.ContainsKey(key.Name))
            {
                values[key.Name] = reader.GetAttribute(key.Name) ?? string.Empty;
            }
        }

        return values;
    }

    private static XmlSorter.SortValue[] CreateSortValues(IReadOnlyDictionary<string, string> attributeValues, IReadOnlyList<SortKey> keys)
    {
        var values = new XmlSorter.SortValue[keys.Count];

        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];
            var value = attributeValues.TryGetValue(key.Name, out var attributeValue) ? attributeValue : string.Empty;
            var numericValue = 0m;
            var isNumeric = key.Numeric && decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out numericValue);
            values[keyIndex] = new XmlSorter.SortValue(value, numericValue, isNumeric);
        }

        return values;
    }

    private static bool RequiresMaterialization(IReadOnlyList<string> childPath, IReadOnlyList<CompiledSortRule> rules)
    {
        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            if (rules[ruleIndex].ParentPathMatcher.CanMatchDescendant(childPath))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class BufferedParentContent
    {
        private readonly List<BufferedParentEntry> entries = [];
        private readonly BufferedTargetRunCollection targets;

        public BufferedParentContent(IReadOnlyList<SortKey> keys)
        {
            targets = new BufferedTargetRunCollection(keys);
        }

        public void AddFixedElement(XElement element)
        {
            entries.Add(BufferedParentEntry.CreateNode(element));
        }

        public void AddRawElement(string rawXml)
        {
            entries.Add(BufferedParentEntry.CreateRaw(rawXml));
        }

        public void AddFixedNode(XNode node)
        {
            entries.Add(BufferedParentEntry.CreateNode(node));
        }

        public void AddTarget(XElement element, IReadOnlyList<SortKey> keys)
        {
            targets.Add(element, XmlSorter.GetSortValues(element, keys));
            entries.Add(BufferedParentEntry.TargetPlaceholder);
        }

        public void AddRawTarget(string rawXml, XmlSorter.SortValue[] values)
        {
            targets.AddRaw(rawXml, values);
            entries.Add(BufferedParentEntry.TargetPlaceholder);
        }

        public void WriteTo(XmlWriter writer)
        {
            using var targetEnumerator = targets.GetMergedEnumerator();

            foreach (var entry in entries)
            {
                if (entry.IsTargetPlaceholder)
                {
                    if (!targetEnumerator.MoveNext())
                    {
                        throw new InvalidOperationException("Buffered target placeholders exceeded the number of buffered targets.");
                    }

                    targetEnumerator.Current.WriteTo(writer);
                    continue;
                }

                entry.WriteTo(writer);
            }
        }
    }

    private sealed class BufferedParentEntry
    {
        private BufferedParentEntry(bool isTargetPlaceholder, XNode? node, string? rawXml)
        {
            IsTargetPlaceholder = isTargetPlaceholder;
            Node = node;
            RawXml = rawXml;
        }

        public static BufferedParentEntry TargetPlaceholder { get; } = new(true, null, null);

        public bool IsTargetPlaceholder { get; }

        public XNode? Node { get; }

        public string? RawXml { get; }

        public static BufferedParentEntry CreateNode(XNode node)
        {
            return new BufferedParentEntry(false, node, null);
        }

        public static BufferedParentEntry CreateRaw(string rawXml)
        {
            return new BufferedParentEntry(false, null, rawXml);
        }

        public void WriteTo(XmlWriter writer)
        {
            if (RawXml is not null)
            {
                writer.WriteRaw(RawXml);
                return;
            }

            Node!.WriteTo(writer);
        }
    }
}
