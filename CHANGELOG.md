# Changelog

## [1.2.0] - Unreleased

### Added
- Numeric sort modifiers for element and attribute keys, including combinations such as `numeric desc`.
- Path wildcard support in sort rules with `*` for single-segment matches and `**` for recursive matches.
- An `XMLSSORT_CONFIG_PATH` environment variable to override the default user-profile configuration path or disable profile lookup.
- A benchmark project covering XML sorting, streaming, file-input loading, and representative workload scenarios.

### Changed
- Matching containers are now sorted recursively by default, which makes a single rule apply consistently across nested matching structures.
- The main processing path now uses streaming XML reads and writes when JSON formatting is not enabled, reducing full-document materialization for file and standard input/output workflows.
- Embedded JSON formatting now skips leaf values that do not begin with JSON object or array syntax.

### Fixed
- Numeric sorts now compare parsed numeric values instead of lexicographic text, while keeping stable tie-breaking behavior.
- Sorting now better preserves interleaved non-target XML content such as comments, processing instructions, CDATA, and sibling nodes during file output.
- Embedded JSON formatting now preserves visible Unicode characters instead of escaping them unnecessarily.
