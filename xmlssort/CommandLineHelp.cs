internal static class CommandLineHelp
{
    public const string Text = """
        Usage:
          xmlssort [input-file | input-pattern | -] ... [--sort <path:key[,key...]> ...] [--format-xml] [--format-json] [--output <file | ->]
          xmlssort <input-file | input-pattern | input-directory> ... [--sort <path:key[,key...]> ...] [--format-xml] [--format-json] (--in-place | --rename | --write-new) [--suffix <text>] [--output-dir <directory>]

        Examples:
          xmlssort books.xml --sort "/Catalog/Books/Book:@id"
          xmlssort books.xml --sort "/Catalog/Books/Book:Author,Title" --output sorted.xml
          type books.xml | xmlssort --sort "/Catalog/Books/Book:@id desc,Title"
          xmlssort library.xml --sort "/Library/Sections/Section:Name"
          xmlssort .\xml --sort "/Catalog/Books/Book:@id" --in-place
          xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --in-place
          xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --write-new --suffix .sorted
          xmlssort .\xml --sort "/Catalog/Books/Book:@id" --write-new --output-dir .\sorted
          xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --rename
          xmlssort books.xml --format-xml
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
          - Wildcard inputs such as `*.xml` are expanded by the application so the same command works on Windows and on shells that do not expand globs automatically.
          - Directory inputs are processed recursively and include `*.xml` files while preserving relative paths for batch output directories.
          - When multiple files are supplied, choose `--in-place`, `--rename`, or `--write-new`.
          - `--rename` moves each sorted result to a sibling file that uses `--suffix` or the default `.sorted` suffix.
          - `--write-new` keeps the original file and writes the sorted result to a sibling file that uses `--suffix` or the default `.sorted` suffix.
          - `--output-dir` writes `--write-new` results into another directory while retaining the input directory structure.
          - Multiple --sort options may target different levels in the hierarchy.
          - Matching nested containers are sorted recursively by default.
          - `--format-xml` writes diff-friendly XML with normalized indentation.
          - `--format-json` pretty-prints valid JSON found in leaf element values.
          - Defaults can be loaded from `~/.xmlssort/config.json` or `%USERPROFILE%\.xmlssort\config.json`.
          - Command-line options override configuration defaults.
        """;
}
