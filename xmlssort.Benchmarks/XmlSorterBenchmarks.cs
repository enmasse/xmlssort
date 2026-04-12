using System.Reflection;
using System.Text;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace xmlssort.Benchmarks;
[CPUUsageDiagnoser]
public class XmlSorterBenchmarks
{
    private Action<XDocument> apply = null!;
    private string xml = string.Empty;
    private XDocument document = null!;
    [Params(40)]
    public int ProductCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var assembly = Assembly.Load("xmlssort");
        var sorterType = assembly.GetType("XmlSorter", throwOnError: true)!;
        var sortRuleType = assembly.GetType("SortRule", throwOnError: true)!;
        var applyMethod = sorterType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var parseMethod = sortRuleType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var rule = parseMethod.Invoke(null, ["/operations/add/product/**/related_items__*/item:id"])!;
        var rules = Array.CreateInstance(sortRuleType, 1);
        rules.SetValue(rule, 0);
        apply = document => applyMethod.Invoke(null, [document, rules]);
        var builder = new StringBuilder();
        builder.Append("<operations><add>");
        for (var productIndex = 0; productIndex < ProductCount; productIndex++)
        {
            builder.Append("<product>");
            builder.Append("<product_key>pk-");
            builder.Append(productIndex);
            builder.Append("</product_key>");
            builder.Append("<related_items__alpha>");
            AppendItems(builder, productIndex * 1000);
            builder.Append("</related_items__alpha>");
            builder.Append("<variants>");
            for (var variantIndex = 0; variantIndex < 4; variantIndex++)
            {
                builder.Append("<variant><id>v-");
                builder.Append(productIndex);
                builder.Append('-');
                builder.Append(variantIndex);
                builder.Append("</id><related_items__beta>");
                AppendItems(builder, productIndex * 1000 + variantIndex * 100);
                builder.Append("</related_items__beta></variant>");
            }

            builder.Append("</variants></product>");
        }

        builder.Append("</add></operations>");
        xml = builder.ToString();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
    }

    [Benchmark]
    public void ApplyWildcardRule()
    {
        apply(document);
    }

    private static void AppendItems(StringBuilder builder, int seed)
    {
        for (var itemIndex = 19; itemIndex >= 0; itemIndex--)
        {
            builder.Append("<item><id>");
            builder.Append(seed + itemIndex);
            builder.Append("</id></item>");
        }
    }
}
