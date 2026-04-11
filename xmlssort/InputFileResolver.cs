using System.Text;
using System.Text.RegularExpressions;

internal static class InputFileResolver
{
    public static IReadOnlyList<InputFileMatch> Resolve(IReadOnlyList<string> inputPaths)
    {
        var resolvedPaths = new List<InputFileMatch>();
        var seenPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var inputPath in inputPaths)
        {
            foreach (var resolvedPath in ResolveSingle(inputPath))
            {
                if (seenPaths.Add(resolvedPath.InputPath))
                {
                    resolvedPaths.Add(resolvedPath);
                }
            }
        }

        return resolvedPaths;
    }

    private static IEnumerable<InputFileMatch> ResolveSingle(string inputPath)
    {
        if (inputPath == "-")
        {
            yield return new InputFileMatch("-", null);
            yield break;
        }

        var fullPath = Path.GetFullPath(inputPath);

        if (Directory.Exists(fullPath))
        {
            foreach (var match in ResolveDirectory(fullPath))
            {
                yield return match;
            }

            yield break;
        }

        if (!ContainsWildcard(inputPath))
        {
            yield return new InputFileMatch(fullPath, Path.GetDirectoryName(fullPath));
            yield break;
        }

        var matches = ExpandPattern(inputPath).ToArray();

        if (matches.Length == 0)
        {
            throw new ArgumentException($"The input pattern '{inputPath}' did not match any files.");
        }

        foreach (var match in matches)
        {
            yield return match;
        }
    }

    private static IEnumerable<InputFileMatch> ResolveDirectory(string directoryPath)
    {
        var matches = Directory
            .EnumerateFiles(directoryPath, "*.xml", SearchOption.AllDirectories)
            .OrderBy(path => path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(path => new InputFileMatch(path, directoryPath))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ArgumentException($"The input directory '{directoryPath}' did not contain any .xml files.");
        }

        return matches;
    }

    private static IEnumerable<InputFileMatch> ExpandPattern(string inputPath)
    {
        var fullPattern = Path.GetFullPath(inputPath);
        var searchRoot = GetSearchRoot(fullPattern);

        if (!Directory.Exists(searchRoot))
        {
            return [];
        }

        var relativePattern = fullPattern[searchRoot.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var regex = BuildPatternRegex(NormalizePath(relativePattern));

        return Directory
            .EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
            .Where(path => regex.IsMatch(NormalizePath(Path.GetRelativePath(searchRoot, path))))
            .OrderBy(path => path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(path => new InputFileMatch(path, searchRoot))
            .ToArray();
    }

    private static bool ContainsWildcard(string inputPath)
    {
        return inputPath.IndexOf('*') >= 0 || inputPath.IndexOf('?') >= 0;
    }

    private static string GetSearchRoot(string fullPattern)
    {
        var wildcardIndex = fullPattern.IndexOfAny(['*', '?']);
        var separatorIndex = fullPattern.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], wildcardIndex);
        var pathRoot = Path.GetPathRoot(fullPattern);

        if (separatorIndex < 0)
        {
            return Directory.GetCurrentDirectory();
        }

        if (!string.IsNullOrEmpty(pathRoot) && separatorIndex < pathRoot.Length)
        {
            return pathRoot;
        }

        if (separatorIndex == 0)
        {
            return fullPattern[..1];
        }

        return fullPattern[..separatorIndex];
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static Regex BuildPatternRegex(string pattern)
    {
        var builder = new StringBuilder("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var current = pattern[i];

            if (current == '*')
            {
                var isDoubleStar = i + 1 < pattern.Length && pattern[i + 1] == '*';

                if (isDoubleStar)
                {
                    var isDirectoryWildcard = i + 2 < pattern.Length && pattern[i + 2] == '/';
                    builder.Append(isDirectoryWildcard ? "(?:.*/)?" : ".*");
                    i += isDirectoryWildcard ? 2 : 1;
                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            if (current == '/')
            {
                builder.Append('/');
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        builder.Append('$');

        return new Regex(builder.ToString(), OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None);
    }
}
