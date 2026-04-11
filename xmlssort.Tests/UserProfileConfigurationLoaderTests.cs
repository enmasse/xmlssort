namespace xmlssort.Tests;

public class UserProfileConfigurationLoaderTests
{
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
}
