# 🌱 Garden Diary

[![CI](https://github.com/reckhou/GardenDiary/actions/workflows/ci.yml/badge.svg)](https://github.com/reckhou/GardenDiary/actions/workflows/ci.yml)

A Windows WPF desktop app to track plant care activities in your garden — because your plants deserve better than a sticky note on the fridge.

> ⚠️ **Disclaimer:** This app was entirely designed, architected, and written by Claude (Anthropic's AI).
> The human "developer" on this project contributed by typing requests and occasionally saying "that works."
> If you're a software developer worried about AI taking your job — yes, you should be. At least in gardening apps.
> On the bright side, someone still needs to do the actual gardening. For now.

---

## Features

### 🌿 Plant Management
- Track **Common Name**, **Latin Name**, and **Variety** per plant
- Full CRUD — add, edit, duplicate, and delete plants with smart numeric suffixes (Tree → Tree 2 → Tree 3)
- Per-plant diary with **Planting, Watering, Fertilizing, Weeding, Mulching, Pruning** and free-text notes
- Assign a **colourful emoji icon** per plant; plants sharing the same emoji get the same background colour
- Filter plants by **area** using a dropdown; plants sorted alphabetically
- **Double-click** any plant to edit; selecting a plant shows a **mini location preview** of where it sits in the garden

### 📅 Calendar View
- Select any day to see all activities across all plants, colour-coded by activity type
- Each activity group has an **Edit** button that opens an activity-level editor — tick/untick any plants for that activity on that day
- **Add Activity** redesigned: choose date, pick an activity, then select multiple plants at once with search/filter and area grouping
- Hovering a plant row in the calendar shows a **location preview** mini-map; each row also displays the plant's garden area
- Notes displayed inline under each plant entry

### 🌤 Weather & Sunrise/Sunset
- Set your **home location** by clicking on an interactive OpenStreetMap map (WebView2)
- Selected date shows **weather condition, temperature range, wind speed & direction, precipitation, sunrise, and sunset** via the free [Open-Meteo](https://open-meteo.com) API
- Covers historical dates back to 1940 (archive API) and forecasts up to 16 days ahead

### 🗺 Garden Planner
- Create named garden **areas** with width × height in cm
- Place plants as **draggable circles** on a canvas; the Place Plant list only shows unplaced plants
- Circles show **common name, latin name, and variety**
- Draw **rectangle and circle shapes** on the canvas — draggable, colour-customisable (preset swatches + custom colour picker), z-order control
- **Grid lines** with cm measurements for reference
- **Zoom** 0.1× – 10× via scroll wheel or buttons; **pan** with middle-mouse drag
- **Double-click** any plant circle to open Add Activity pre-filled with today's date
- Mouse interaction help strip always visible in the planner

### 💾 Backup & Restore
- Manual backup on demand and **automatic daily backup** on startup
- Both `data.json` and `areas.json` are backed up together
- **Restore** from any backup — select either the data file or the areas file and the other is found automatically
- A safety backup is created before any restore

### ⚙️ Data & Persistence
- JSON storage in `%AppData%\GardenDiary\`
- Last selected garden area remembered across sessions
- Settings (backup path, home location) stored in `settings.json`

---

## Requirements

- Windows 10/11
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- Microsoft Edge WebView2 Runtime (pre-installed on Windows 11; available via Windows Update on Windows 10)

## Getting Started

### Download

Grab the latest self-contained `.exe` from the [Releases](https://github.com/reckhou/GardenDiary/releases) page — no .NET install required.

### Build from source

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
├── Models/            Plant, DiaryEntry, AppSettings, DayWeather,
│                      DayTaskGroup, PlantSummary, GardenArea, PlantPlacement, GardenShape
├── ViewModels/        MainViewModel, RelayCommand, PlantOption, PlantFilterItem
├── Views/             PlantEditDialog, DiaryEntryEditDialog, CalendarEntryDialog,
│                      EditActivityDialog, BackupSettingsDialog, GardenAreaEditDialog,
│                      LocationPickerDialog
├── Helpers/           GardenPreviewHelper
├── Services/          DataService, BackupService, WeatherService
├── Converters/        StringToBrushConverter
└── MainWindow.xaml

GardenDiary.Tests/
├── DataServiceTests.cs
└── BackupServiceTests.cs

.github/workflows/
├── ci.yml             Build + test on every push/PR
└── release.yml        Publish self-contained win-x64 exe on version tags
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

## Garden Planner Controls

| Input | Action |
|---|---|
| Left-click empty canvas | Place selected plant |
| Left-click circle | Select plant |
| Drag circle | Move plant |
| Double-click circle | Open Add Activity dialog |
| Left-click shape | Select shape |
| Drag shape | Move shape |
| Right-click shape | Z-order menu (bring to front / send to back) |
| Middle-mouse drag | Pan canvas |
| Scroll wheel | Zoom in / out |
| −  +  ↺ buttons | Zoom out / in / reset |

## License

[WTFPL](LICENSE) — Do What The F*** You Want To Public License.

Much like this project itself.
