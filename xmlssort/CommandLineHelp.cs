internal static class CommandLineHelp
{
    public const string Text = """
        Usage:
          xmlssort [input-file | -] [--sort <path:key[,key...]> ...] [--format-json] [--output <file | ->]

        Examples:
          xmlssort books.xml --sort "/Catalog/Books/Book:@id"
          xmlssort books.xml --sort "/Catalog/Books/Book:Author,Title" --output sorted.xml
          type books.xml | xmlssort --sort "/Catalog/Books/Book:@id desc,Title"
          xmlssort library.xml --sort "/Library/Sections/Section:Name"
          xmlssort payloads.xml --format-json
          xmlssort books.xml

        Sort rule format:
          /Root/Parent/Item:key1,key2

        Keys:
          Name       Sort by a direct child element named Name
          @id        Sort by an attribute named id
          Title desc Sort descending by a direct child element named Title

        Notes:
          - Input defaults to stdin when no input file is supplied or when '-' is used.
          - Output defaults to stdout when --output is omitted or when '--output -' is used.
          - Multiple --sort options may target different levels in the hierarchy.
          - Matching nested containers are sorted recursively by default.
          - `--format-json` pretty-prints valid JSON found in leaf element values.
          - Defaults can be loaded from `~/.xmlssort/config.json` or `%USERPROFILE%\.xmlssort\config.json`.
          - Command-line options override configuration defaults.
        """;
}
