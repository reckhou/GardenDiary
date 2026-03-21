# Garden Diary

## Project Overview

A Windows WPF desktop app to track plant care activities in your garden. Features plant management, calendar view, weather integration, garden planner with drag-and-drop placement, and automated backup/restore.

**Tech Stack**: C# .NET 9, WPF, WebView2 (OpenStreetMap integration), JSON persistence

## Global Rules

This project follows the global Claude Code rules defined in the [ShanesClaudeCodeGlobalRules](https://github.com/reckhou/ShanesClaudeCodeGlobalRules) repository.

## Project-Specific Guidelines

**Version Source of Truth**: `<Version>` in `GardenDiary/GardenDiary.csproj`

Update the version number in the `.csproj` file as part of every version commit.

## Architecture Principles

- **MVVM Pattern**: Strict separation between Models, ViewModels, and Views
- **Services Layer**: DataService (JSON persistence), BackupService (auto/manual backups), WeatherService (Open-Meteo API)
- **No Business Logic in UI**: ViewModels orchestrate all logic; Views are binding-only
- **JSON Storage**: `%AppData%\GardenDiary\` for data.json, areas.json, settings.json
- **Transactional Backups**: Before any restore, a safety backup is created automatically

## Development Notes

- Test framework: xUnit with 15 passing tests covering DataService and BackupService
- CI/CD: GitHub Actions runs tests on every push/PR; release workflow publishes self-contained exe on version tags
- Target: Windows 10/11 with .NET 9 Runtime and WebView2 Runtime
