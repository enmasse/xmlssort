using System.Diagnostics;
using System.Runtime.InteropServices;

namespace xmlssort.Tests;

[NotInParallel]
public class XmldiffApplicationTests
{
    private static string? _binaryPath;
    private static DirectoryInfo? _publishDirectory;

    [Before(Class)]
    public static async Task PublishBinary()
    {
        var solutionRoot = FindSolutionRoot();
        var projectPath = Path.Combine(solutionRoot, "xmldiff", "xmldiff.csproj");
        _publishDirectory = Directory.CreateTempSubdirectory("xmldiff-integration-");
        var rid = GetPortableRid();

        var (exitCode, _, stderr) = await RunProcessAsync(
            "dotnet",
            ["publish", projectPath, "--configuration", "Release", "--runtime", rid, "--output", _publishDirectory.FullName],
            workingDirectory: solutionRoot,
            standardInput: null,
            environmentVariables: null);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to publish xmldiff binary for rid '{rid}':{Environment.NewLine}{stderr}");
        }

        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "xmldiff.exe" : "xmldiff";
        _binaryPath = Path.Combine(_publishDirectory.FullName, binaryName);

        if (!File.Exists(_binaryPath))
        {
            throw new FileNotFoundException($"Published binary not found at: {_binaryPath}");
        }
    }

    [After(Class)]
    public static void CleanupPublishDirectory()
    {
        try
        {
            _publishDirectory?.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public async Task Binary_ReportsCorrelatedDifferencesByChosenKeys()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"2\"><Title>Beta</Title></Book><Book id=\"1\" status=\"draft\"><Title>Alpha</Title></Book></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\" status=\"published\"><Title>Alpha revised</Title></Book><Book id=\"3\"><Title>Gamma</Title></Book></Books></Catalog>");

            var (exitCode, stdout, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/Catalog/Books/Book:@id"],
                configurationPath: string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("Changed: 1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Only in left: 1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Only in right: 1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("@id=1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("@id=2", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("@id=3", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Title: left='Alpha', right='Alpha revised'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("@status: left='draft', right='published'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Left: <Book id=\"1\"", StringComparison.Ordinal)).IsFalse();
            await Assert.That(stdout.Contains("Right: <Book id=\"1\"", StringComparison.Ordinal)).IsFalse();
            await Assert.That(stdout.Contains("Gamma", StringComparison.Ordinal)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_WritesHtmlReportWhenRequested()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            var reportPath = Path.Combine(tempDirectory.FullName, "report.html");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"2\"><Title>Beta</Title></Book><Book id=\"1\" status=\"draft\"><Title>Alpha</Title></Book></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\" status=\"published\"><Title>Alpha revised</Title></Book><Book id=\"3\"><Title>Gamma</Title></Book></Books></Catalog>");

            var (exitCode, _, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/Catalog/Books/Book:@id", "--report-format", "html", "--output", reportPath],
                configurationPath: string.Empty);
            var html = await File.ReadAllTextAsync(reportPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(html.Contains("<html", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(html.Contains("@id=1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("@id=2", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("@id=3", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("Comments: ignored", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("Tag</th><th>Left</th><th>Right</th>", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("Title", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("@status", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("Alpha revised", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("published", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("&lt;Book id=&quot;2&quot;&gt;", StringComparison.Ordinal)).IsFalse();
            await Assert.That(html.Contains("&lt;Book id=&quot;3&quot;&gt;", StringComparison.Ordinal)).IsFalse();
            await Assert.That(html.Contains("Gamma", StringComparison.Ordinal)).IsFalse();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_IgnoresXmlCommentsByDefault()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"1\"><Title>Alpha</Title><!-- keep --></Book></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\"><Title>Alpha</Title></Book></Books></Catalog>");

            var (exitCode, stdout, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/Catalog/Books/Book:@id"],
                configurationPath: string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("No differences found.", StringComparison.Ordinal)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_UsesSharedConfigurationFileWhenCommandLineOmitsSortRules()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            var configPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(leftPath, "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>");
            await File.WriteAllTextAsync(rightPath, "<Catalog><Books><Book id=\"1\"/><Book id=\"2\"/></Books></Catalog>");
            await File.WriteAllTextAsync(configPath, """
                {
                  "sort": [
                    "/Catalog/Books/Book:@id"
                  ]
                }
                """);

            var (exitCode, stdout, stderr) = await RunBinaryAsync([leftPath, rightPath], configurationPath: configPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("No differences found.", StringComparison.Ordinal)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_TreatsSameSortKeyInDifferentWildcardScopesAsDistinctReportKeys()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(leftPath, "<operations><add><product><product_key>pk-1</product_key><Title>Left add</Title></product></add><update><product><product_key>pk-1</product_key><Title>Left update</Title></product></update></operations>");
            await File.WriteAllTextAsync(rightPath, "<operations><add><product><product_key>pk-1</product_key><Title>Right add</Title></product></add><update><product><product_key>pk-1</product_key><Title>Right update</Title></product></update></operations>");

            var (exitCode, stdout, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/operations/*/product:product_key"],
                configurationPath: string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("Duplicate keys: 0", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("operations[1]/add[1] | product_key=pk-1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("operations[1]/update[1] | product_key=pk-1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Title: left='Left add', right='Right add'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Title: left='Left update', right='Right update'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_StillReportsDuplicateKeysWithinTheSameScope()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(leftPath, "<operations><add><product><product_key>pk-1</product_key><Title>A</Title></product><product><product_key>pk-1</product_key><Title>B</Title></product></add></operations>");
            await File.WriteAllTextAsync(rightPath, "<operations><add><product><product_key>pk-1</product_key><Title>C</Title></product></add></operations>");

            var (exitCode, stdout, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/operations/*/product:product_key"],
                configurationPath: string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("Duplicate keys: 1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("Duplicate key: operations[1]/add[1] | product_key=pk-1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Binary_ShowsNestedVariantDifferencesUsingVariantKeysWithinProductDiffs()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var leftPath = Path.Combine(tempDirectory.FullName, "left.xml");
            var rightPath = Path.Combine(tempDirectory.FullName, "right.xml");
            await File.WriteAllTextAsync(
                leftPath,
                "<operations><add><product><product_key>pk-1</product_key><variants><variant><variant_key>vk-1</variant_key><Price>10</Price></variant></variants></product></add></operations>");
            await File.WriteAllTextAsync(
                rightPath,
                "<operations><add><product><product_key>pk-1</product_key><variants><variant><variant_key>vk-1</variant_key><Price>20</Price></variant></variants></product></add></operations>");

            var (exitCode, stdout, stderr) = await RunBinaryAsync(
                [leftPath, rightPath, "--sort", "/operations/*/product:product_key", "--sort", "/operations/add/product/variants/variant:variant_key"],
                configurationPath: string.Empty);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(stdout.Contains("Changed: operations[1]/add[1] | product_key=pk-1", StringComparison.Ordinal)).IsTrue();
            await Assert.That(stdout.Contains("variants/variant_key=vk-1/Price: left='10', right='20'", StringComparison.Ordinal)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private static Task<(int ExitCode, string StandardOutput, string StandardError)> RunBinaryAsync(
        string[] args,
        string configurationPath)
    {
        return RunProcessAsync(
            _binaryPath!,
            args,
            workingDirectory: null,
            standardInput: null,
            environmentVariables: new Dictionary<string, string>
            {
                [UserProfileConfigurationLoader.ConfigurationPathEnvironmentVariableName] = configurationPath,
            });
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
        string fileName,
        string[] args,
        string? workingDirectory,
        string? standardInput,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? string.Empty,
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        process.Start();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
        }

        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string GetPortableRid()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var archSeparator = rid.LastIndexOf('-');
        if (archSeparator < 0)
        {
            return rid;
        }

        var arch = rid[(archSeparator + 1)..];
        var platformPart = rid[..archSeparator];
        var platformSeparator = platformPart.IndexOf('.');
        if (platformSeparator >= 0)
        {
            platformPart = platformPart[..platformSeparator];
        }

        return $"{platformPart}-{arch}";
    }

    private static string FindSolutionRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "xmlssort.sln")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Unable to locate solution root.");
    }
}
