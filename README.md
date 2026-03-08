# 🌱 Garden Diary

[![CI](https://github.com/reckhou/GardenDiary/actions/workflows/ci.yml/badge.svg)](https://github.com/reckhou/GardenDiary/actions/workflows/ci.yml)

A Windows WPF desktop app to track plant care activities in your garden — because your plants deserve better than a sticky note on the fridge.

> ⚠️ **Disclaimer:** This app was entirely designed, architected, and written by Claude (Anthropic's AI).
> The human "developer" on this project contributed by typing requests and occasionally saying "that works."
> If you're a software developer worried about AI taking your job — yes, you should be. At least in gardening apps.
> On the bright side, someone still needs to do the actual gardening. For now.

---

## Features

- **Plant management** — track Common Name, Latin Name, and Variety
- **Diary entries per plant** — log Planting, Watering, Fertilizing, Weeding, Mulching, and Pruning with notes
- **Calendar view** — select any day to see all activities across all plants, colour-coded by activity type
- **Backup** — manual backup on demand and automatic daily backup to a folder of your choice
- **JSON persistence** — data stored in `%AppData%\GardenDiary\data.json`

## Screenshots

| Calendar View | Plants & Diary |
|---|---|
| Activities grouped by type with colour-coded badges | Full CRUD for plants and diary entries |

## Requirements

- Windows 10/11
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)

## Getting Started

```bash
git clone https://github.com/reckhou/GardenDiary.git
cd GardenDiary
dotnet run --project GardenDiary
```

Or open `GardenDiary.sln` in Visual Studio 2022+.

## Running Tests

```bash
dotnet test --configuration Release --verbosity normal
```

### Test Results

```
Total tests: 15
     Passed: 15
 Total time: ~0.37 seconds
```

| Suite | Tests |
|---|---|
| `DataServiceTests` | Load empty, save/load plant properties, diary entries roundtrip, multiple plants, overwrite |
| `BackupServiceTests` | Auto-backup scheduling, file naming (manual vs auto), missing file/path errors, date tracking |

CI runs automatically on every push and pull request via GitHub Actions on `windows-latest`.

## Project Structure

```
GardenDiary/
├── Models/            Plant, DiaryEntry, AppSettings, DayTaskGroup, PlantSummary
├── ViewModels/        MainViewModel, RelayCommand
├── Views/             PlantEditDialog, DiaryEntryEditDialog, CalendarEntryDialog, BackupSettingsDialog
├── Services/          DataService, BackupService
├── Converters/        StringToBrushConverter
└── MainWindow.xaml

GardenDiary.Tests/
├── DataServiceTests.cs
└── BackupServiceTests.cs

.github/workflows/
└── ci.yml             Build + test on every push/PR
```

## Activity Colours

| Activity | Colour |
|---|---|
| 🌱 Planting | Green |
| 💧 Watering | Blue |
| 🧪 Fertilizing | Orange |
| 🌿 Weeding | Brown |
| 🪵 Mulching | Amber |
| ✂️ Pruning | Purple |

## License

[WTFPL](LICENSE) — Do What The F*** You Want To Public License.

Much like this project itself.
