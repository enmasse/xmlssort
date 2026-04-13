using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class UserProfileConfigurationLoader(string? configurationPath = null) : IUserConfigurationLoader
{
    internal const string ConfigurationPathEnvironmentVariableName = "XMLSSORT_CONFIG_PATH";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public UserConfiguration? Load()
    {
        var path = configurationPath ?? GetDefaultConfigurationPath();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<UserConfigurationFile>(json, SerializerOptions)
                ?? new UserConfigurationFile();

            var sortRules = (file.Sort ?? [])
                .Select(SortRule.Parse)
                .ToArray();

            return new UserConfiguration(sortRules, file.FormatXml ?? false, file.FormatJson ?? false, file.SortTags ?? false);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid configuration file '{path}': {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Invalid configuration file '{path}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Unable to read configuration file '{path}': {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Unable to read configuration file '{path}': {ex.Message}", ex);
        }
    }

    public static string? GetDefaultConfigurationPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ConfigurationPathEnvironmentVariableName);

        if (configuredPath is not null)
        {
            return string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath;
        }

        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(userProfilePath))
        {
            return null;
        }

        return Path.Combine(userProfilePath, ".xmlssort", "config.json");
    }

    private sealed class UserConfigurationFile
    {
        [JsonPropertyName("sort")]
        public string[]? Sort { get; init; }

        [JsonPropertyName("formatJson")]
        public bool? FormatJson { get; init; }

        [JsonPropertyName("formatXml")]
        public bool? FormatXml { get; init; }

        [JsonPropertyName("sortTags")]
        public bool? SortTags { get; init; }
    }
}
