# TODO

## Roadmap

### Near term
- [ ] Harden command-line validation and error messages for invalid sort rules, missing files, and conflicting output options.
- [ ] Expand automated coverage for end-to-end sorting workflows, especially batch processing, wildcard rules, and formatting combinations.
- [ ] Document recommended usage patterns for recursive sorting, numeric keys, wildcard paths, and configuration overrides.
- [ ] Add branch protection-friendly status naming to the GitHub Actions workflows.
- [ ] Add manual release workflow inputs to choose which platforms to publish.
- [ ] Add artifact retention settings for CI and release workflow artifacts.

### Mid term
- [ ] Add Windows Explorer integration so users can sort XML files from the shell context menu.
- [ ] Package a Windows-friendly installer that can register the command-line tool and optional Explorer integration.
- [ ] Add a dry-run or preview mode that reports intended file changes without writing output.

### Long term
- [ ] Add a lightweight desktop UI for composing sort rules and previewing sorted output.
- [ ] Add extensibility points for reusable rule presets and team-shared configuration profiles.
- [ ] Investigate cross-platform file-manager integrations after the Windows shell workflow is established.
