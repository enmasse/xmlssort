using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace xmlssort.Tests;

public class BinaryIntegrationTests
{
    private static string? _binaryPath;
    private static DirectoryInfo? _publishDirectory;

    [Before(Class)]
    public static async Task PublishBinary()
    {
        var solutionRoot = FindSolutionRoot();
        var projectPath = Path.Combine(solutionRoot, "xmlssort", "xmlssort.csproj");
        _publishDirectory = Directory.CreateTempSubdirectory("xmlssort-integration-");
        var rid = GetPortableRid();

        var (exitCode, _, stderr) = await RunProcessAsync(
            "dotnet",
            ["publish", projectPath, "--configuration", "Release", "--runtime", rid, "--output", _publishDirectory.FullName],
            workingDirectory: solutionRoot,
            standardInput: null,
            environmentVariables: null);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to publish xmlssort binary for rid '{rid}':{Environment.NewLine}{stderr}");
        }

        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "xmlssort.exe" : "xmlssort";
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
            // Best-effort cleanup
        }
    }

    [Test]
    public async Task Binary_SortsXmlFromStdinToStdout()
    {
        const string input = "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>";

        var (exitCode, stdout, stderr) = await RunBinaryAsync(
            ["--sort", "/Catalog/Books/Book:@id"],
            standardInput: input);

        var document = XDocument.Parse(stdout);
        var ids = document.Root!
            .Element("Books")!
            .Elements("Book")
            .Select(book => book.Attribute("id")!.Value)
            .ToArray();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Binary_SortsXmlFileToOutputFile()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.xml");
            var outputPath = Path.Combine(tempDirectory.FullName, "output.xml");

            await File.WriteAllTextAsync(inputPath, "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>");

            var (exitCode, _, stderr) = await RunBinaryAsync(
                [inputPath, "--sort", "/Catalog/Books/Book:@id", "--output", outputPath],
                standardInput: null);

            var output = XDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var ids = output.Root!
                .Element("Books")!
                .Elements("Book")
                .Select(book => book.Attribute("id")!.Value)
                .ToArray();

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Binary_SortsXmlFileInPlace()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "input.xml");

            await File.WriteAllTextAsync(inputPath, "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>");

            var (exitCode, _, stderr) = await RunBinaryAsync(
                [inputPath, "--sort", "/Catalog/Books/Book:@id", "--in-place"],
                standardInput: null);

            var output = XDocument.Parse(await File.ReadAllTextAsync(inputPath));
            var ids = output.Root!
                .Element("Books")!
                .Elements("Book")
                .Select(book => book.Attribute("id")!.Value)
                .ToArray();

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join("|", ids)).IsEqualTo("1|2");
            await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Binary_WritesHelpToStdout()
    {
        var (exitCode, stdout, stderr) = await RunBinaryAsync(["--help"], standardInput: null);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(stdout.Contains("Usage:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(string.IsNullOrWhiteSpace(stderr)).IsTrue();
    }

    [Test]
    public async Task Binary_ReturnsNonZeroExitCodeForInvalidXml()
    {
        var (exitCode, _, stderr) = await RunBinaryAsync(
            ["--sort", "/Catalog/Books/Book:@id"],
            standardInput: "<Catalog>");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(stderr.Contains("unexpected end", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    /// <summary>
    /// Confirms the end-to-end behaviour when no configuration file exists and no --sort argument
    /// is provided: the binary exits with code 1 and writes a descriptive error to stderr, producing
    /// no stdout output.  This was the root cause of the osx-arm64 "no output" report — the user's
    /// config was absent in the test environment, so no operations were configured and the tool
    /// rejected the invocation silently (from a stdout perspective).
    /// </summary>
    [Test]
    public async Task Binary_ProducesNoStdoutAndExitsWithErrorWhenNoOperationsAreConfigured()
    {
        const string input = "<Catalog><Books><Book id=\"2\"/><Book id=\"1\"/></Books></Catalog>";

        // Run with a file argument but no --sort, --format-xml, or --format-json, and with the
        // config path explicitly cleared so no user profile configuration can be loaded.
        var (exitCode, stdout, stderr) = await RunBinaryAsync(
            args: [],
            standardInput: input);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(string.IsNullOrWhiteSpace(stdout)).IsTrue();
        await Assert.That(stderr.Contains("At least one operation is required", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    private static Task<(int ExitCode, string StandardOutput, string StandardError)> RunBinaryAsync(
        string[] args,
        string? standardInput)
    {
        return RunProcessAsync(
            _binaryPath!,
            args,
            workingDirectory: null,
            standardInput: standardInput,
            environmentVariables: new Dictionary<string, string>
            {
                // Prevent any user profile config from interfering with integration tests.
                [UserProfileConfigurationLoader.ConfigurationPathEnvironmentVariableName] = string.Empty,
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

        // Strip OS version numbers so we get a portable RID the SDK can publish for.
        // e.g. "osx.14.0-arm64" → "osx-arm64", "linux.ubuntu.22.04-x64" → "linux-x64"
        var archSeparator = rid.LastIndexOf('-');
        if (archSeparator < 0)
        {
            return rid;
        }

        var osPart = rid[..archSeparator];
        var archPart = rid[archSeparator..];

        var dotIndex = osPart.IndexOf('.');
        if (dotIndex >= 0)
        {
            osPart = osPart[..dotIndex];
        }

        return osPart + archPart;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "xmlssort.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the solution root directory containing xmlssort.sln.");
    }
}
