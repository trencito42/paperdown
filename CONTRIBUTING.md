# Contributing to Paperdown

Thank you for helping improve Paperdown.

## Development setup

1. Install .NET 8 SDK and WebView2 Runtime.
2. Clone the repository and run `dotnet build Paperdown.sln -c Debug -p:Platform=x64`.
3. Run tests: `dotnet test tests/Paperdown.Tests/Paperdown.Tests.csproj -c Release`.

### Visual Studio (F5)

- Open `Paperdown.sln` or `Paperdown.slnx`.
- Set configuration to **Debug** and platform **x64** (not *Any CPU*).
- Set startup project to **Paperdown.App**.
- Use the **Paperdown (Unpackaged)** debug profile (standard `.exe` launch, no MSIX deploy).
- If you see missing `Paperdown.Core.dll` warnings, run **Build → Rebuild Solution** once.

## Pull requests

- Keep changes focused and include tests for rendering, emoji, or PDF behavior when relevant.
- Do not commit personal documents or proprietary PDFs—use synthetic fixtures under `tests/Paperdown.Tests/Fixtures/`.
- Verify PDF changes with the integration test or a manual export on Windows.

## Code style

- Follow existing project structure and naming.
- Prefer extending shared rendering code over duplicating HTML/CSS in the WinUI layer.
