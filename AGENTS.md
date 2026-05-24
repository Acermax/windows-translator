# Agent Notes

## Current State
- This is a Windows-only .NET 8 WPF solution for a selected-text translator.
- Main projects: `src/WindowsTranslator.App` for WPF/Win32 integration and `src/WindowsTranslator.Core` for testable settings/translation logic.
- Tests live in `tests/WindowsTranslator.Tests` and reference only `WindowsTranslator.Core`; keep UI/Win32 behavior thin in the app project.
- Selected-text capture is intentionally non-invasive via Windows UI Automation; do not add automatic `SendInput`, `Ctrl+C`, or clipboard fallback without explicit user approval.
- The workspace path contains a space (`windows translator`); quote paths in PowerShell commands.

## Commands
- Restore: `dotnet restore WindowsTranslator.sln`
- Build: `dotnet build WindowsTranslator.sln`
- Test all: `dotnet test WindowsTranslator.sln`
- Run app: `dotnet run --project src\WindowsTranslator.App\WindowsTranslator.App.csproj`
- If build cannot overwrite `src\WindowsTranslator.App\bin\Debug\net8.0-windows\WindowsTranslator.Core.dll`, a running `WindowsTranslator.App` tray process is locking it; close the tray app or build to a temporary `-o` folder.
