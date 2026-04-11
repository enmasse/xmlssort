internal static class BatchOutputPathResolver
{
    public const string DefaultSuffix = ".sorted";

    public static string ResolveSibling(string inputPath, string suffix)
    {
        var directory = Path.GetDirectoryName(inputPath);
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);

        return Path.Combine(directory ?? string.Empty, $"{fileName}{suffix}{extension}");
    }

    public static string ResolveOutputDirectoryPath(InputFileMatch inputFile, string outputDirectory, string suffix)
    {
        var relativePath = GetRelativePath(inputFile);
        var relativeDirectory = Path.GetDirectoryName(relativePath);
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        var extension = Path.GetExtension(relativePath);
        var outputFileName = $"{fileName}{suffix}{extension}";

        return string.IsNullOrEmpty(relativeDirectory)
            ? Path.Combine(outputDirectory, outputFileName)
            : Path.Combine(outputDirectory, relativeDirectory, outputFileName);
    }

    private static string GetRelativePath(InputFileMatch inputFile)
    {
        if (string.IsNullOrWhiteSpace(inputFile.RootPath))
        {
            return Path.GetFileName(inputFile.InputPath);
        }

        return Path.GetRelativePath(inputFile.RootPath, inputFile.InputPath);
    }
}
