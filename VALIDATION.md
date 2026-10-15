# Validation — 0.5.0

- Release build: zero warnings and errors.
- 50 unit/integration tests pass on Windows, including FTS migration, Turkish folding, filesystem updates, launch gating, preview routing, navigation boundaries and null settings normalization.
- NuGet vulnerability check including transitive dependencies: no known vulnerable packages reported by the configured NuGet source at check time.
- Fixed search-to-results Down skipping the first row; Up now enters at the last row.
- Null collections/empty hotkeys in settings are normalized before consumers run.
- Portrait image previews now constrain the longest decoded edge; small images are not unnecessarily enlarged.
- Toolbar uses shared 20px vector geometry, consistent strokes and rounded joins. Settings uses sliders, files use a single page icon.
- Clipboard history and its listener remain removed.

Earlier UI smoke checks verified the compact toolbar, real PNG preview and duration of a generated one-second AVI. This release's portrait preview and final toolbar still require interactive acceptance. Codec coverage, full-drive scale, every Windows DPI configuration and installed upgrade/uninstall behavior are not exhaustively tested. Passing tests are not a claim that the application has no bugs.

MSI extraction and portable payload hashes are verified before delivery. Local profiles, databases, logs, packaged binaries and scratch files are excluded from the repository.
