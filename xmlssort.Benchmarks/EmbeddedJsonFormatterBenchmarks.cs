using System.Reflection;
using System.Text;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace xmlssort.Benchmarks;
[CPUUsageDiagnoser]
public class EmbeddedJsonFormatterBenchmarks
{
    private Action<XDocument> apply = null!;
    private string xml = string.Empty;
    private XDocument document = null!;
    [Params(5000)]
    public int ElementCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var assembly = Assembly.Load("xmlssort");
        var formatterType = assembly.GetType("EmbeddedJsonFormatter", throwOnError: true)!;
        var method = formatterType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        apply = document => method.Invoke(null, [document]);
        var builder = new StringBuilder();
        builder.Append("<Root>");
        for (var i = 0; i < ElementCount; i++)
        {
            builder.Append("<Value>");
            builder.Append("plain text ");
            builder.Append(i);
            builder.Append("</Value>");
        }

        builder.Append("<Payload>{\"name\":\"Alice\",\"roles\":[\"admin\",\"user\"]}</Payload>");
        builder.Append("<Payload>[1,2,3,4]</Payload>");
        builder.Append("</Root>");
        xml = builder.ToString();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        document = XDocument.Parse(xml);
    }

    [Benchmark]
    public void Apply()
    {
        apply(document);
    }
}
