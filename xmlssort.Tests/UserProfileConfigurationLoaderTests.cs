namespace xmlssort.Tests;

public class UserProfileConfigurationLoaderTests
{
    [Test]
    public async Task GetDefaultConfigurationPath_UsesEnvironmentOverride()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configPath = Path.Combine(tempDirectory.FullName, "config.json");
            using var environmentVariableScope = new EnvironmentVariableScope(UserProfileConfigurationLoader.ConfigurationPathEnvironmentVariableName, configPath);

            var resolvedPath = UserProfileConfigurationLoader.GetDefaultConfigurationPath();

            await Assert.That(resolvedPath).IsEqualTo(configPath);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Load_ReturnsNullWhenEnvironmentOverrideDisablesUserProfileLookup()
    {
        using var environmentVariableScope = new EnvironmentVariableScope(UserProfileConfigurationLoader.ConfigurationPathEnvironmentVariableName, string.Empty);

        var configuration = new UserProfileConfigurationLoader().Load();

        await Assert.That(configuration is null).IsTrue();
    }

    [Test]
    public async Task Load_ReturnsConfigurationFromJsonFile()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(
                configPath,
                """
                {
                  "sort": [
                    "/Catalog/Books/Book:@id"
                  ],
                  "formatXml": true,
                  "formatJson": true
                }
                """);

            var configuration = new UserProfileConfigurationLoader(configPath).Load();

            await Assert.That(configuration is not null).IsTrue();
            await Assert.That(configuration!.SortRules.Count).IsEqualTo(1);
            await Assert.That(configuration.SortRules[0].TargetElementName).IsEqualTo("Book");
            await Assert.That(configuration.FormatXml).IsTrue();
            await Assert.That(configuration.FormatJson).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Load_ReturnsNullWhenConfigurationFileDoesNotExist()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configPath = Path.Combine(tempDirectory.FullName, "missing.json");

            var configuration = new UserProfileConfigurationLoader(configPath).Load();

            await Assert.That(configuration is null).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Load_ThrowsForInvalidConfigurationJson()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(configPath, "{ invalid json }");
            var threw = false;
            var message = string.Empty;

            try
            {
                _ = new UserProfileConfigurationLoader(configPath).Load();
            }
            catch (ArgumentException ex)
            {
                threw = true;
                message = ex.Message;
            }

            await Assert.That(threw).IsTrue();
            await Assert.That(message.Contains("Invalid configuration file", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Test]
    public async Task Load_ThrowsForInvalidSortRuleInConfiguration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var configPath = Path.Combine(tempDirectory.FullName, "config.json");
            await File.WriteAllTextAsync(
                configPath,
                """
                {
                  "sort": [
                    "Catalog/Books/Book:@id"
                  ]
                }
                """);

            var threw = false;
            var message = string.Empty;

            try
            {
                _ = new UserProfileConfigurationLoader(configPath).Load();
            }
            catch (ArgumentException ex)
            {
                threw = true;
                message = ex.Message;
            }

            await Assert.That(threw).IsTrue();
            await Assert.That(message.Contains("Invalid configuration file", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string variableName;
        private readonly string? originalValue;

        public EnvironmentVariableScope(string variableName, string? value)
        {
            this.variableName = variableName;
            originalValue = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }
}
