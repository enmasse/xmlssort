using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace xmlssort.Benchmarks;
[CPUUsageDiagnoser]
public class XmlSortedFileInputBenchmarks
{
    private object application = null!;
    private MethodInfo runMethod = null!;
    private string inputPath = string.Empty;
    private string outputPath = string.Empty;
    private string workingDirectory = string.Empty;
    private string? originalConfigurationPath;
    [Params(4000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        originalConfigurationPath = Environment.GetEnvironmentVariable("XMLSSORT_CONFIG_PATH");
        var assembly = Assembly.Load("xmlssort");
        var applicationType = assembly.GetType("XmlSortApplication", throwOnError: true)!;
        var constructor = applicationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        application = constructor.Invoke([null ]);
        runMethod = applicationType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public)! ?? throw new InvalidOperationException("XmlSortApplication.Run was not found.");
        workingDirectory = Path.Combine(Path.GetTempPath(), "xmlssort-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        inputPath = Path.Combine(workingDirectory, "input.xml");
        outputPath = Path.Combine(workingDirectory, "output.xml");
        Environment.SetEnvironmentVariable("XMLSSORT_CONFIG_PATH", Path.Combine(workingDirectory, "missing.config.json"));
        var builder = new StringBuilder();
        builder.Append("<Catalog><Books>");
        for (var index = ItemCount - 1; index >= 0; index--)
        {
            builder.Append("<Book id=\"");
            builder.Append(index);
            builder.Append("\"><Title>Title ");
            builder.Append(index);
            builder.Append("</Title><Description>");
            builder.Append('x', 128);
            builder.Append("</Description></Book>");
        }

        builder.Append("</Books></Catalog>");
        File.WriteAllText(inputPath, builder.ToString());
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
    public int RunWithSortedFileInput()
    {
        var exitCode = (int)runMethod.Invoke(application, [new[] { inputPath, "--sort", "/Catalog/Books/Book:@id", "--output", outputPath }])!;
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"XmlSortApplication returned exit code {exitCode}.");
        }

        return exitCode;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Environment.SetEnvironmentVariable("XMLSSORT_CONFIG_PATH", originalConfigurationPath);
        if (Directory.Exists(workingDirectory))
        {
            Directory.Delete(workingDirectory, true);
        }
    }
}
