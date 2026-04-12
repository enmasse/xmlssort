using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace xmlssort.Benchmarks;

[CPUUsageDiagnoser]
public class XmlRealWorkloadStreamingBenchmarks
{
    private const string InputPathEnvironmentVariable = "XMLSSORT_BENCHMARK_REAL_INPUT_PATH";
    private const string SortRuleEnvironmentVariable = "XMLSSORT_BENCHMARK_REAL_SORT_RULE";
    private const string DefaultSortRule = "/Catalog/Books/Book:@id";
    private const int PrefixEntryCount = 20000;
    private const int BookCount = 50000;
    private const int DescriptionLength = 512;

    private MethodInfo processMethod = null!;
    private object options = null!;
    private string inputPath = string.Empty;
    private string outputPath = string.Empty;
    private string workingDirectory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        workingDirectory = Path.Combine(Path.GetTempPath(), "xmlssort-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        outputPath = Path.Combine(workingDirectory, "output.xml");
        inputPath = ResolveInputPath();

        if (!File.Exists(inputPath))
        {
            throw new InvalidOperationException($"The XML workload file '{inputPath}' does not exist.");
        }

        var sortRules = (Environment.GetEnvironmentVariable(SortRuleEnvironmentVariable) ?? DefaultSortRule)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var assembly = Assembly.Load("xmlssort");
        var commandLineOptionsType = assembly.GetType("CommandLineOptions", throwOnError: true)!;
        var xmlStreamingProcessorType = assembly.GetType("XmlStreamingProcessor", throwOnError: true)!;
        var parseMethod = commandLineOptionsType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("CommandLineOptions.Parse was not found.");
        processMethod = xmlStreamingProcessorType.GetMethod(
            "Process",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(string), commandLineOptionsType],
            modifiers: null)
            ?? throw new InvalidOperationException("XmlStreamingProcessor.Process was not found.");
        options = parseMethod.Invoke(null, [BuildArguments(sortRules)])
            ?? throw new InvalidOperationException("CommandLineOptions.Parse returned null.");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Benchmark]
    public void ProcessRealWorkload()
    {
        processMethod.Invoke(null, [inputPath, outputPath, options]);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(workingDirectory))
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    private string ResolveInputPath()
    {
        var configuredInputPath = Environment.GetEnvironmentVariable(InputPathEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(configuredInputPath))
        {
            return configuredInputPath;
        }

        var generatedInputPath = Path.Combine(workingDirectory, "representative-input.xml");
        WriteRepresentativeWorkload(generatedInputPath);
        return generatedInputPath;
    }

    private static void WriteRepresentativeWorkload(string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 131072);
        writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?><Catalog><ImportHistory>");

        for (var index = 0; index < PrefixEntryCount; index++)
        {
            writer.Write("<Entry id=\"");
            writer.Write(index);
            writer.Write("\"><Source>feed-");
            writer.Write(index % 32);
            writer.Write("</Source><Checkpoint>");
            writer.Write(index * 17);
            writer.Write("</Checkpoint><Status>Complete</Status></Entry>");
        }

        writer.Write("</ImportHistory><Books>");

        for (var index = BookCount - 1; index >= 0; index--)
        {
            writer.Write("<Book id=\"");
            writer.Write(index);
            writer.Write("\"><Title>Title ");
            writer.Write(index);
            writer.Write("</Title><Author>Author ");
            writer.Write(index % 1000);
            writer.Write("</Author><Description>");
            writer.Write(new string('x', DescriptionLength));
            writer.Write("</Description><Metadata><Edition>");
            writer.Write((index % 7) + 1);
            writer.Write("</Edition><Category>Category ");
            writer.Write(index % 64);
            writer.Write("</Category></Metadata></Book>");
        }

        writer.Write("</Books><AuditTrail>");

        for (var index = 0; index < 1024; index++)
        {
            writer.Write("<Event sequence=\"");
            writer.Write(index);
            writer.Write("\">processed</Event>");
        }

        writer.Write("</AuditTrail></Catalog>");
    }

    private static string[] BuildArguments(IReadOnlyList<string> sortRules)
    {
        var arguments = new List<string>(sortRules.Count * 2);

        for (var index = 0; index < sortRules.Count; index++)
        {
            arguments.Add("--sort");
            arguments.Add(sortRules[index]);
        }

        return [.. arguments];
    }
}
