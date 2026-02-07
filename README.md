# OpenPlane

## 1. What This Is
OpenPlane is a .NET MAUI desktop app (macOS + Windows) that uses GitHub Copilot SDK to run prompt-driven cowork tasks with model selection, login status, execution diagnostics, and timeline output.

## 2. Runtime Controls
- Execution mode:
  - Embedded CLI process (default)
  - External Copilot endpoint (`CliUrl`)
- Auth diagnostics panel:
  - effective command/endpoint
  - CLI version
  - model-list probe status
  - last startup error
- Device login support:
  - parsed user code + verification URL
  - copy code + open verification page
- Workspace grant controls:
  - add/remove allowed folders
  - local file tools enforce grants
  - blocked operations are surfaced as policy violations

## 3. Local File Tool Prompt Format
Use `tool:` prompts to run scoped local file operations:
- `tool:read|/absolute/path/file.txt`
- `tool:search|/absolute/path/root|*.cs`
- `tool:write|/absolute/path/file.txt|new content`
- `tool:create-file|/absolute/path/new.txt|content`
- `tool:create-folder|/absolute/path/new-folder`

## 4. How To Run On Mac
1. Prerequisites:
   - .NET SDK 10
   - Xcode (full app, not just Command Line Tools)
2. Ensure Xcode is selected:
   - `xcode-select -p` should be `/Applications/Xcode.app/Contents/Developer`
3. Build:
   - `dotnet build OpenPlane.sln`
4. Run:
   - `dotnet build src/OpenPlane.App/OpenPlane.App.csproj -t:Run -f net10.0-maccatalyst`

## 5. How To Run On Windows
1. Prerequisites:
   - .NET SDK 10
   - Visual Studio 2022/2026 with MAUI and Windows App SDK workloads
2. Build:
   - `dotnet build OpenPlane.sln`
3. Run:
   - `dotnet build src/OpenPlane.App/OpenPlane.App.csproj -t:Run -f net10.0-windows10.0.19041.0`
