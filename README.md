# xmlssort

`xmlssort` is a .NET 9 console application for sorting XML documents while preserving the document hierarchy.

It supports:
- file input or `stdin`
- multiple file inputs and wildcard patterns for batch processing
- recursive directory input for batch processing
- `stdout` output or writing to a file
- JSON configuration defaults from the user profile
- optional canonical XML formatting for diff-friendly output
- sorting all sibling elements alphabetically by tag name with `--sort-tags`
- sorting by direct child element values
- sorting by attribute values
- optional formatting of embedded JSON in element values
- multiple sort keys
- multiple sort rules for different parts of the document
- recursive sorting of matching nested containers by default

## Example

Input:

```xml
<Catalog>
  <Books>
    <Book id="2">
      <Title>Zebra</Title>
    </Book>
    <Book id="1">
      <Title>Alpha</Title>
    </Book>
  </Books>
</Catalog>
```

Command:

```powershell
xmlssort books.xml --sort "/Catalog/Books/Book:@id"
```

Output:

```xml
<?xml version="1.0" encoding="utf-8"?><Catalog>
  <Books>
    <Book id="1">
      <Title>Alpha</Title>
    </Book>
    <Book id="2">
      <Title>Zebra</Title>
    </Book>
  </Books>
</Catalog>
```

## Command line usage

```text
xmlssort [input-file | input-pattern | -] ... [--sort <path:key[,key...]> ...] [--sort-tags] [--format-xml] [--format-json] [--output <file | ->]
xmlssort <input-file | input-pattern | input-directory> ... [--sort <path:key[,key...]> ...] [--sort-tags] [--format-xml] [--format-json] (--in-place | --rename | --write-new) [--suffix <text>] [--output-dir <directory>]
```

If command-line operations are omitted, defaults can come from the user profile configuration file.

### Input and output

- If no input file is supplied, the tool reads from `stdin`.
- `-` can be used explicitly for `stdin` or `stdout`.
- If `--output` is omitted, the sorted XML is written to `stdout`.
- Wildcard inputs such as `*.xml` are expanded by the application so the same command works on Windows and shells that do not expand globs automatically.
- Directory inputs are processed recursively and include `*.xml` files.
- When multiple files are supplied, use `--in-place`, `--rename`, or `--write-new`.
- `--rename` moves each sorted result to a sibling file using `--suffix` or the default `.sorted` suffix.
- `--write-new` keeps the original file and writes a sibling file using `--suffix` or the default `.sorted` suffix.
- `--output-dir` can be used with `--write-new` to write results into another directory while preserving the input directory structure.

Examples:

```powershell
xmlssort books.xml --sort "/Catalog/Books/Book:@id"
xmlssort books.xml --sort "/Catalog/Books/Book:Author,Title" --output sorted.xml
Get-Content books.xml | xmlssort --sort "/Catalog/Books/Book:@id desc,Title"
xmlssort .\xml --sort "/Catalog/Books/Book:@id" --in-place
xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --in-place
xmlssort .\xml --sort "/Catalog/Books/Book:@id" --write-new --output-dir .\sorted
xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --write-new --suffix .sorted
xmlssort "*.xml" --sort "/Catalog/Books/Book:@id" --rename
xmlssort books.xml --format-xml
xmlssort payloads.xml --format-json
xmlssort books.xml
```

## Sort rule format

```text
/Root/Parent/Item:key1,key2
```

- `/Root/Parent/Item` selects the sibling elements to reorder.
- `key1,key2` defines the sort precedence.
- Keys can target either direct child elements or attributes.

### Key syntax

- `Name` sorts by a direct child element named `Name`
- `@id` sorts by an attribute named `id`
- `sortorder numeric` sorts numerically by a direct child element named `sortorder`
- `@rank numeric desc` sorts numerically in descending order by an attribute named `rank`
- `Title desc` sorts descending
- `Title asc` sorts ascending explicitly

Examples:

```text
/Catalog/Books/Book:@id
/Catalog/Books/Book:Author,Title
/Catalog/Books/Book:Price numeric
/Catalog/Books/Book:Title desc
/Library/Sections/Section:Name
```

### Path wildcards

- `*` matches characters within a single path segment
- `**` matches zero or more whole path segments

Examples:

```text
/operations/add/product/related_items__*/item:id
/operations/add/product/**/related_items__*/item:id
```

In the second example, one rule sorts `item` elements under matching `related_items__*` containers both directly on `product` and deeper under structures such as `variants/variant`.

### Different keys at different levels

You can apply multiple `--sort` rules to use different keys in different parts of the hierarchy.

Example input:

```xml
<Library>
  <Sections>
    <Section>
      <Name>Fiction</Name>
      <Books>
        <Book id="2">
          <Title>Zebra</Title>
        </Book>
        <Book id="1">
          <Title>Alpha</Title>
        </Book>
      </Books>
    </Section>
    <Section>
      <Name>Art</Name>
      <Books>
        <Book id="4">
          <Title>Modern</Title>
        </Book>
        <Book id="3">
          <Title>Classic</Title>
        </Book>
      </Books>
    </Section>
  </Sections>
</Library>
```

Command:

```powershell
xmlssort library.xml --sort "/Library/Sections/Section:Name" --sort "/Library/Sections/Section/Books/Book:@id"
```

In this example:

- `Section` elements are sorted by `Name`
- `Book` elements inside each `Books` element are sorted by `@id`

This lets each hierarchy level use the key that makes sense for that level.

## Canonical XML formatting

Use `--format-xml` to rewrite the output with normalized indentation so it is easier to compare in standard diff tools.

- The flag is global.
- It applies when writing output.
- Indentation-only whitespace between nested elements is normalized.
- Leaf text values are left intact.

Example:

Input:

```xml
<Catalog><Books><Book id="2" /><Book id="1" /></Books></Catalog>
```

Command:

```powershell
xmlssort books.xml --sort "/Catalog/Books/Book:@id" --format-xml
```

Output:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Catalog>
  <Books>
    <Book id="1" />
    <Book id="2" />
  </Books>
</Catalog>
```

## Embedded JSON formatting

Use `--format-json` to pretty-print valid JSON found in leaf element values.

- The flag is global.
- It applies after sorting.
- Only leaf elements are considered.
- Invalid JSON is left unchanged.

Example:

Input:

```xml
<Root>
  <Payload>{"name":"Alice","roles":["admin"]}</Payload>
</Root>
```

Command:

```powershell
xmlssort payloads.xml --format-json
```

Output:

```xml
<?xml version="1.0" encoding="utf-8"?><Root>
  <Payload>{
  "name": "Alice",
  "roles": [
    "admin"
  ]
}</Payload>
</Root>
```

The option can also be combined with sorting:

```powershell
xmlssort payloads.xml --sort "/Catalog/Books/Book:@id" --format-json
```

## User profile configuration

Defaults can be loaded from a JSON configuration file in the user profile.

- Windows: `%USERPROFILE%\.xmlssort\config.json`
- Linux/macOS: `~/.xmlssort/config.json`

You can override the default lookup path with `XMLSSORT_CONFIG_PATH`.

- Set it to an explicit file path to load configuration from another location.
- Set it to an empty value to disable user-profile configuration lookup.

Missing configuration is ignored.

Command-line options override configuration defaults.

Supported settings:

- `sort` - array of default sort rules
- `formatXml` - enables canonical XML formatting by default
- `formatJson` - enables embedded JSON formatting by default

Example:

```json
{
  "sort": [
    "/Catalog/Books/Book:@id"
  ],
  "formatXml": true,
  "formatJson": true
}
```

With this configuration, the following command uses the configured defaults:

```powershell
xmlssort books.xml
```

You can still override the defaults on the command line:

```powershell
xmlssort books.xml --sort "/Catalog/Books/Book:Title desc"
```

## Recursive behavior

Matching nested containers are sorted recursively by default.

For example, this rule:

```text
/Library/Sections/Section:Name
```

sorts:
- top-level `Section` elements under `Sections`
- nested `Section` elements under nested `Sections` containers as well

You can still provide multiple `--sort` rules when different hierarchy levels need different keys.

## Project structure

- `xmlssort/Program.cs` - entry point
- `xmlssort/XmlSortApplication.cs` - application flow and console/file I/O
- `xmlssort/CommandLineOptions.cs` - command-line parsing
- `xmlssort/CommandLineOptionsResolver.cs` - configuration-aware option resolution
- `xmlssort/CanonicalXmlFormatter.cs` - diff-friendly XML output formatting
- `xmlssort/CommandLineHelp.cs` - CLI help text
- `xmlssort/IUserConfigurationLoader.cs` - configuration loading abstraction
- `xmlssort/SortRule.cs` - sort rule parsing
- `xmlssort/SortKey.cs` - sort key parsing
- `xmlssort/XmlSorter.cs` - XML sorting behavior
- `xmlssort/UserConfiguration.cs` - configuration defaults model
- `xmlssort/UserProfileConfigurationLoader.cs` - profile-based JSON config loading
- `xmlssort/SortKeyKind.cs` - key type enum
- `xmlssort/SortDirection.cs` - sort direction enum
- `xmlssort.Tests/` - TUnit test project

## Building

```powershell
dotnet build
```

## Publishing a single executable

`xmlssort` is configured to publish as a single self-contained executable.

Publish for Windows x64:

```powershell
dotnet publish .\xmlssort\xmlssort.csproj -c Release -r win-x64
```

Publish for Linux x64:

```powershell
dotnet publish .\xmlssort\xmlssort.csproj -c Release -r linux-x64
```

Publish for macOS arm64:

```powershell
dotnet publish .\xmlssort\xmlssort.csproj -c Release -r osx-arm64
```

The executable is written to a runtime-specific publish folder under `xmlssort\bin\Release\net9.0\<rid>\publish\`.

This uses standard single-file publishing, so it does not require Native AOT toolchain prerequisites.

## Running

```powershell
dotnet run --project .\xmlssort\xmlssort.csproj -- books.xml --sort "/Catalog/Books/Book:@id"
```

Using `stdin`:

```powershell
Get-Content books.xml | dotnet run --project .\xmlssort\xmlssort.csproj -- --sort "/Catalog/Books/Book:@id"
```

## Testing

The repository includes a TUnit test project covering command-line parsing, XML sorting behavior, recursive sorting, and application-level flows.

Build the solution:

```powershell
dotnet build
```

Run the tests with the TUnit runner:

```powershell
dotnet run --project .\xmlssort.Tests\xmlssort.Tests.csproj
```
