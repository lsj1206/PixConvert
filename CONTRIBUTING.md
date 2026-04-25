# Contributing to PixConvert

Thanks for considering a contribution to PixConvert.

## Development Environment

- Windows
- .NET 10 SDK
- Visual Studio, VS Code, or another C# editor with WPF support

## Build and Test

Run these commands before opening a pull request:

```powershell
dotnet restore PixConvert.sln
dotnet build PixConvert.sln -c Release --no-restore -v minimal -m:1
dotnet test PixConvert.Tests\PixConvert.Tests.csproj -c Release --no-restore --no-build -v minimal
```

## Pull Request Checklist

- Keep changes focused on one problem or feature.
- Add or update tests when behavior changes.
- Update README files when user-facing behavior changes.
- Update `THIRD-PARTY-NOTICES.md` when dependency or release-package contents change.
- Confirm the release package impact if the change affects publishing, native libraries, icons, or metadata.

## Issue Guidelines

Use the bug report template for reproducible defects and the feature request template for new behavior. Include PixConvert version, Windows version, input/output formats, and screenshots or logs when relevant.
