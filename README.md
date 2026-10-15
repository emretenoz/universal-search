# Universal Search

A local desktop search launcher for Windows 10/11 x64, built with C#/.NET 10, WPF, MVVM and SQLite FTS5.

## Features

- Search selected folders, Start menu shortcuts and installed applications.
- Turkish/English UI, light/dark themes, compact vector toolbar and keyboard navigation.
- Incremental filesystem watching with periodic reconciliation, filename filters and bounded fuzzy matching.
- Favorites, local recent searches, calculator, configurable web searches and optional browser-history import.
- Space-key previews for supported images and videos; file metadata for other files.
- Tray operation, configurable global shortcut and optional startup shortcut.

Clipboard history is not included.

## Run

Install the per-user x64 MSI or extract the portable ZIP if supplied with a release. Published builds include the .NET runtime. No administrator elevation is requested by the installer.

Press **Alt+Space** to open search. Add locations in Settings before searching personal files. The Start menu and installed apps are available automatically. Use the arrow keys to select a result, Enter to open it, and Esc to hide search. With a file result focused, Space opens its preview. Space remains a normal space while typing in the search box.

Examples:

```text
report ext:pdf
notes in:"C:\Work"
type:folder
is:favorite
12 * (8 + 2)
> settings
g search terms
```

Right-click a result to open its location, copy its path or toggle its favorite state. Clicking an active category returns to all results.

## Develop

Requires Windows x64 and the .NET 10 SDK. From the repository root:

```powershell
dotnet restore UniversalSearch.sln
dotnet build UniversalSearch.sln -c Release
dotnet test UniversalSearch.sln -c Release
dotnet run --project src/UniversalSearch.App -- --data-dir .dev-data
```

Use an isolated data directory and a different global shortcut when testing alongside an installed copy.

## Package

The installer uses WiX 4 and PowerShell 7. Keep generated packages out of Git:

```powershell
dotnet tool install wix --tool-path .tools --version '4.*'
./installer/Build-Installer.ps1 -Wix ./.tools/wix.exe
```

The script publishes a self-contained Release build to `publish/` and builds an MSI in `dist/`. Updates preserve application data. Close the installed app before upgrading. Do not run test installers over a production installation to validate packaging.

## Structure

- `src/UniversalSearch.Core`: search models, arithmetic, matching and navigation policies.
- `src/UniversalSearch.Infrastructure`: SQLite index, filesystem watching, settings and optional browser imports.
- `src/UniversalSearch.App`: WPF views/view model, native hotkey/tray integration, shell launching and previews.
- `tests/UniversalSearch.Tests`: unit and filesystem/database integration tests.
- `installer`: repeatable per-user installer generation.

## Privacy and limitations

Settings, index, favorites and recent queries are stored under `%LOCALAPPDATA%\UniversalSearch` by default. Search does not upload them. Explicit web searches and opening browser results use the configured browser and search provider. Browser import is off by default and requires opt-in. Index/history cleanup is available in Settings; personal files are not deleted.

This is a filename/metadata search tool, not a full document-content indexer. Full-drive initial scans can be slow; exclude system/cache directories. Reparse points and offline files are skipped. Fuzzy matching uses a bounded candidate set. Browser imports are snapshots and locked databases may be unavailable. Application deduplication requires verified matching identities, so some shortcuts may still appear twice. Video support depends on Windows codecs and a preview frame can be black. Image preview input is limited to 64 MiB, with its longest decoded edge limited to 1,000 pixels. No malware scanning or password detection is provided.

See [VALIDATION.md](VALIDATION.md) for the verified scope. Third-party attribution is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`. No open-source license for the application itself is granted by this private repository.
