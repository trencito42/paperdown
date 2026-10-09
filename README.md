# Paperdown

> **Beautiful Markdown. Perfect PDFs.**

Paperdown is a privacy-first, offline **WinUI 3** desktop app for Windows 11 that converts Markdown into professionally typeset PDFs with reliable Unicode, emoji (Twemoji SVG), tables, code blocks, and print-aware CSS themes.

**Version:** 0.1.0-beta

## Why Paperdown exists

Many Markdown → PDF tools render emoji as missing-glyph squares, break pagination, or ship as heavy web wrappers. Paperdown is a native Windows utility that uses the same HTML/CSS pipeline for **live preview** and **WebView2 `PrintToPdfAsync`** export—selectable text, local assets, no cloud.

## Features (0.1.0-beta)

- Native WinUI 3 shell (Mica, system theme, title bar integration)
- Split editor + WebView2 paginated preview
- Markdig (GFM, tables, task lists, alerts, YAML front matter)
- Five document themes: Minimal, GitHub, Documentation, Corporate, Dark
- Bundled Twemoji SVG emoji pipeline for PDF-safe rendering (offline)
- Branding panel: logo, cover page, presets, page numbers
- Page settings: A4 / Letter, orientation, margins
- PDF export with replace confirmation, open file/folder actions
- CLI: `paperdown convert README.md -o README.pdf --theme github`
- xUnit tests including WebView2 PDF export integration on Windows

## Screenshots

_Screenshots will be added after the first public release build._

## Prerequisites

- Windows 10 1809+ or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (usually preinstalled on Windows 11)
- Visual Studio 2022 17.8+ with **.NET Desktop Development** and **Windows application development** workloads (optional, for IDE debugging)

## Build from source

```powershell
git clone https://github.com/your-org/paperdown.git
cd paperdown
dotnet restore Paperdown.sln
dotnet build Paperdown.sln -c Release -p:Platform=x64
dotnet test tests/Paperdown.Tests/Paperdown.Tests.csproj -c Release
```

### Run the desktop app

```powershell
dotnet run --project src/Paperdown.App/Paperdown.App.csproj -c Release
```

### CLI

```powershell
dotnet run --project src/Paperdown.Cli/Paperdown.Cli.csproj -c Release -- convert README.md -o README.pdf --theme minimal
```

### Portable publish (x64)

```powershell
.\scripts\publish-portable.ps1
```

Output: `artifacts/portable/win-x64/Paperdown.App/`

## Solution layout

```
src/Paperdown.App        WinUI 3 UI
src/Paperdown.Core       Models, preferences, branding presets
src/Paperdown.Markdown   Markdig pipeline + sanitization
src/Paperdown.Rendering  HTML templates, CSS themes, emoji assets
src/Paperdown.Pdf        WebView2 PDF export service
src/Paperdown.Cli        Command-line converter
tests/Paperdown.Tests    Unit + PDF integration tests
```

## Privacy

No accounts, telemetry, analytics, or network calls during conversion. Markdown is sanitized (scripts/iframes stripped). WebView2 navigation to remote HTTP(S) URLs is blocked in preview.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Application source code: [MIT](LICENSE).  
Bundled emoji graphics: [Twemoji CC-BY 4.0](THIRD_PARTY_NOTICES.md).  
Other dependencies: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
