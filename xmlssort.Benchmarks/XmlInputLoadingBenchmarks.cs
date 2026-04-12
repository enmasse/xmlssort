using System.Reflection;
using System.Text;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace xmlssort.Benchmarks;
[CPUUsageDiagnoser]
public class XmlInputLoadingBenchmarks
{
    private object application = null!;
    private MethodInfo runMethod = null!;
    private string inputPath = string.Empty;
    private string outputPath = string.Empty;
    private string workingDirectory = string.Empty;
    [Params(4000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var assembly = Assembly.Load("xmlssort");
        var applicationType = assembly.GetType("XmlSortApplication", throwOnError: true)!;
        var constructor = applicationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        application = constructor.Invoke([null]);
        runMethod = applicationType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public)!;
        workingDirectory = Path.Combine(Path.GetTempPath(), "xmlssort-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        inputPath = Path.Combine(workingDirectory, "input.xml");
        outputPath = Path.Combine(workingDirectory, "output.xml");
        var builder = new StringBuilder();
        builder.Append("<Catalog><Books>");
        for (var index = 0; index < ItemCount; index++)
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
    public int RunWithFileInput()
    {
        var exitCode = (int)runMethod.Invoke(application, [new[] { inputPath, "--sort", "/Other/Item:@id", "--output", outputPath }])!;
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"XmlSortApplication returned exit code {exitCode}.");
        }

        return exitCode;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(workingDirectory))
        {
            Directory.Delete(workingDirectory, true);
        }
    }
}
