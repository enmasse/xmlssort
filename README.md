# xmlssort

`xmlssort` is a .NET 9 console application for sorting XML documents while preserving the document hierarchy.

The repository also includes `xmldiff`, a companion .NET 9 console application that sorts matching XML elements by the same keys and then reports real differences between two XML documents.

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
- `xmldiff/Program.cs` - diff tool entry point
- `xmldiff.Core/` - shared xmldiff comparison and report generation library
- `xmldiff/DiffCommandLineOptions.cs` - diff command-line parsing
- `xmldiff/DiffCommandLineHelp.cs` - diff CLI help text
- `xmldiff.UI/` - Windows UI for xmldiff with drag-and-drop input and embedded report preview
- `xmlssort.Tests/` - TUnit test project

## xmldiff

`xmldiff` compares two XML files after applying the same keyed sort rules used by `xmlssort`.

- It reads `sort`, `formatJson`, `formatXml`, and `sortTags` defaults from the same configuration file as `xmlssort`.
- At least one keyed `--sort` rule is required so matching elements can be correlated.
- Reports can be written as text or HTML.

### xmldiff UI

`xmldiff.UI` is a Windows Forms front end for `xmldiff`.

- Drag one or two XML files onto the window to populate the left and right inputs.
- Review or change the shared defaults loaded from the xmlssort configuration file.
- Preview either the HTML report or the text report inside the embedded Windows Forms browser control.
- Optionally save the generated report to `.html` or `.txt`.

Example:

```powershell
xmldiff left.xml right.xml --sort "/Catalog/Books/Book:@id" --report-format html --output report.html
```

## Building

```powershell
dotnet build
```

Build the Windows installer:

```powershell
.\build.ps1
```

The build script regenerates the installer branding assets, reads the installer version from `xmlssort\xmlssort.csproj`, publishes self-contained Windows binaries for:

- `xmldiff.UI`
- `xmldiff`
- `xmlssort`

and then builds a platform-specific MSI such as `artifacts\installer\win-x64\xmlssort-installer-x64.msi`.

The MSI exposes installer-time options for:

- per-user or per-machine installation
- changing the install path
- installing the `xmldiff` UI
- installing the CLI tools
- adding the CLI install directory to `PATH`
- creating Start Menu shortcuts for the UI

Optional build script parameters:

```powershell
.\build.ps1 -Platform x64
.\build.ps1 -Platform x86
.\build.ps1 -VersionSourceProject .\xmldiff\xmldiff.csproj
.\build.ps1 -InstallerVersion 1.4.1
```

Optional code-signing parameters:

```powershell
.\build.ps1 -Platform x64 -SignArtifacts -CertificateThumbprint <thumbprint>
.\build.ps1 -Platform x64 -SignArtifacts -CertificateFilePath .\certificates\codesign.pfx -CertificatePassword <password>
```

When signing is enabled, the build script signs the published executables first and then signs the generated MSI. You can override the signing tool path with `-SignToolPath` and the timestamp service with `-TimestampUrl`.

CI-friendly signing environment variables:

- `XMLSSORT_SIGN_ARTIFACTS`
- `XMLSSORT_SIGNTOOL_PATH`
- `XMLSSORT_CERTIFICATE_THUMBPRINT`
- `XMLSSORT_CERTIFICATE_FILE_PATH`
- `XMLSSORT_CERTIFICATE_PASSWORD`
- `XMLSSORT_TIMESTAMP_URL`

Examples:

```powershell
$env:XMLSSORT_SIGN_ARTIFACTS = 'true'
$env:XMLSSORT_CERTIFICATE_THUMBPRINT = '<thumbprint>'
.\build.ps1 -Platform x64
```

```powershell
$env:XMLSSORT_CERTIFICATE_FILE_PATH = '.\certificates\codesign.pfx'
$env:XMLSSORT_CERTIFICATE_PASSWORD = '<password>'
.\build.ps1 -Platform x64
```

Explicit script arguments override environment variables. If `XMLSSORT_SIGN_ARTIFACTS` is not set, the build script automatically enables signing when a certificate thumbprint or certificate file path is provided through the environment.

## CI and releases

The GitHub Actions CI workflow:

- runs on pushes to `master`
- runs on pull requests
- is filtered to repository paths that affect the build, tests, installer, or workflows
- builds the installer for `x64` and `x86`
- uploads the MSI files and packaged Windows publish outputs as workflow artifacts

The GitHub Actions release workflow:

- runs when a tag matching `v*` is pushed
- builds the existing cross-platform CLI release binaries
- builds the Windows installers for `x64` and `x86`
- publishes all generated binaries, MSI files, and packaged Windows publish outputs to the GitHub release for that tag

Optional GitHub secrets and variables for CI signing:

- secret `XMLSSORT_CERTIFICATE_THUMBPRINT`
- secret `XMLSSORT_CERTIFICATE_PFX_BASE64`
- secret `XMLSSORT_CERTIFICATE_PASSWORD`
- variable `XMLSSORT_SIGNTOOL_PATH`
- variable `XMLSSORT_TIMESTAMP_URL`

When `XMLSSORT_CERTIFICATE_PFX_BASE64` is used, the workflow writes the decoded certificate to a temporary `.pfx` file and passes that path to `build.ps1` through the signing environment variables.

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

Publish `xmldiff` for Windows x64:

```powershell
dotnet publish .\xmldiff\xmldiff.csproj -c Release -r win-x64
```

Each executable is written to a runtime-specific publish folder under its project directory, for example `xmlssort\bin\Release\net9.0\<rid>\publish\` or `xmldiff\bin\Release\net9.0\<rid>\publish\`.

This uses standard single-file publishing, so it does not require Native AOT toolchain prerequisites.

## Running

```powershell
dotnet run --project .\xmlssort\xmlssort.csproj -- books.xml --sort "/Catalog/Books/Book:@id"
```

Using `stdin`:

```powershell
Get-Content books.xml | dotnet run --project .\xmlssort\xmlssort.csproj -- --sort "/Catalog/Books/Book:@id"
```

Run `xmldiff`:

```powershell
dotnet run --project .\xmldiff\xmldiff.csproj -- left.xml right.xml --sort "/Catalog/Books/Book:@id"
```

Run `xmldiff.UI`:

```powershell
dotnet run --project .\xmldiff.UI\xmldiff.UI.csproj
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
