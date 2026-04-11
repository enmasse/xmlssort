internal sealed record InputFileMatch(string InputPath, string? RootPath)
{
    public bool IsStandardInput => InputPath == "-";
}
