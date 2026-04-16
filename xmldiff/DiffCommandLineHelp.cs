internal static class DiffCommandLineHelp
{
    public const string Text = """
        Usage:
          xmldiff <left-file> <right-file> [--sort <path:key[,key...]> ...] [--sort-tags] [--format-xml] [--format-json] [--report-format <text | html>] [--output <file | ->]

        Examples:
          xmldiff left.xml right.xml --sort "/Catalog/Books/Book:@id"
          xmldiff left.xml right.xml --sort "/Catalog/Books/Book:@id" --report-format html --output report.html
          xmldiff left.xml right.xml --sort "/Catalog/Books/Book:@id" --sort-tags

        Notes:
          - xmldiff reads sort, format-json, format-xml, and sort-tags defaults from the same xmlssort configuration file.
          - At least one keyed --sort rule is required so matching elements can be correlated by the chosen keys.
          - XML comments are ignored by default when determining differences.
          - --format-xml affects how XML fragments are rendered inside the report; formatting-only differences are ignored during comparison.
          - --report-format defaults to text unless the output path ends with .html.
        """;
}
