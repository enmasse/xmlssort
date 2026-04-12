# Copilot Instructions

## Project Guidelines
- User prefers code to be structured with stronger architectural separation rather than keeping all logic in a single file.
- User considers data from real XML samples private and does not want sample-derived information leaked into repository tests or documentation. Only repository tests should be sanitized to avoid leaking private sample data.
- Tests in this repository should not read configuration from the user's profile; they must use isolated configuration sources instead.

## Performance Analysis
- When analyzing performance in this repository, treat JSON pretty printing and user-configuration loading as separate concerns from XML file loading unless the measured execution path explicitly includes them.
- For performance work in this repository, prioritize wall-clock time improvements over CPU load reduction when evaluating optimizations.
- Treat XML formatting and embedded JSON formatting as separate concerns that can run in a second pass or separate application when optimizing the main XML sorting path.
- For XML sorting performance work, optimize primarily for sorting by product key and variant key; other sorting behavior is secondary.