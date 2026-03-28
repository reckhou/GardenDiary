# Calendar Improvements Implementation Plan

**Goal:** Enhance the calendar day panel with foldable activity groups, alphabetical sorting, emoji weather icons with text, expanded weather data (cloud cover, sunshine hours, radiation), and wind gust speed.

**Architecture:** All weather data flows through `WeatherService → DayWeather model → MainViewModel computed properties → XAML bindings`. Activity grouping lives in `DayTaskGroup` (model) + `LoadDayTasks()` (VM) + ItemsControl templates (XAML). Changes are additive: extend the model, add VM properties, update XAML. No new files needed.

**Tech Stack:** C# .NET 9, WPF, Open-Meteo API

---

## Progress

- [x] Task 1: Foldable activity groups + alphabetical sort
- [x] Task 2: Extended weather data (gusts, cloud cover, sunshine hours, radiation)
- [x] Task 3: Weather panel UI redesign (emoji + text, 2-line layout, larger text)

---

## Files

- Modify: `GardenDiary/Models/DayWeather.cs` — add WindGust, CloudCover, SunshineHours, ShortwaveRadiation fields
- Modify: `GardenDiary/Models/DayTaskGroup.cs` — add IsExpanded property + INotifyPropertyChanged
- Modify: `GardenDiary/Services/WeatherService.cs` — add new fields to API URL + parse them; add WMO→emoji mapping
- Modify: `GardenDiary/ViewModels/MainViewModel.cs` — add WeatherWind gust, WeatherCloudCover, WeatherSunshineHours, WeatherRadiation computed strings; sort plants alphabetically in LoadDayTasks; fire OnPropertyChanged for new props
- Modify: `GardenDiary/MainWindow.xaml` — foldable activity group headers; weather WrapPanel → 2-line Grid with emoji+text labels; larger font

---

### Task 1: Foldable activity groups + alphabetical sort

**Files:** `GardenDiary/Models/DayTaskGroup.cs`, `GardenDiary/ViewModels/MainViewModel.cs`, `GardenDiary/MainWindow.xaml`

**DayTaskGroup** — add `IsExpanded` bool (default true) + INotifyPropertyChanged so toggle works via binding:
```csharp
public class DayTaskGroup : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }
    // ... existing props unchanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
```

**MainViewModel.LoadDayTasks()** — sort plants alphabetically before adding:
```csharp
.OrderBy(p => p.Name)
.ToList()
```
Add this `.OrderBy` before `.ToList()` in the LINQ chain that builds the plants list.

**XAML activity group template** — wrap the plant list in a collapsible section. The activity badge row becomes a clickable toggle button:
- Replace the badge `<Border>` with a `<Button>` (flat style) that has a `▶/▼` chevron + the badge inline
- Bind the plant list `Visibility` to `IsExpanded` via BoolToVis converter
- The Edit button stays on the right of the header row

Pattern:
```xml
<!-- Header row: chevron + badge + Edit button -->
<Grid Margin="0,0,0,4">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>  <!-- chevron -->
        <ColumnDefinition Width="*"/>     <!-- badge -->
        <ColumnDefinition Width="Auto"/>  <!-- Edit -->
    </Grid.ColumnDefinitions>
    <Button Grid.Column="0" Content="{Binding IsExpanded, Converter=...chevron...}"
            Command="{Binding ToggleExpandedCommand}" ... flat style />
    <Border Grid.Column="1" ...badge... />
    <Button Grid.Column="2" Content="Edit" ... />
</Grid>
<!-- Collapsible plant list -->
<ItemsControl ItemsSource="{Binding Plants}"
              Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVis}}"/>
```

Since `DayTaskGroup` is a model (no command infrastructure), use a Click handler in code-behind instead of a command for the toggle — it calls `group.IsExpanded = !group.IsExpanded` directly.

**Verify:** Open calendar, select a day with multiple activity types. Each group shows a ▼ chevron. Click it — plants list collapses. Click again — expands. Plants within each group are in A–Z order.

---

### Task 2: Extended weather data (gusts, cloud cover, sunshine, radiation)

**Files:** `GardenDiary/Models/DayWeather.cs`, `GardenDiary/Services/WeatherService.cs`, `GardenDiary/ViewModels/MainViewModel.cs`

**DayWeather** — add fields:
```csharp
public double WindGust { get; set; }         // km/h max gust
public double CloudCover { get; set; }        // % mean
public double SunshineHours { get; set; }     // hours
public double ShortwaveRadiation { get; set; } // MJ/m²
```

**WeatherService** — extend API URL to include new daily variables:
```
wind_gusts_10m_max,cloud_cover_mean,sunshine_duration,shortwave_radiation_sum
```
Note: `sunshine_duration` is in seconds — divide by 3600 for hours.

Parse them in the return statement:
```csharp
WindGust            = GetFirstDouble(daily, "wind_gusts_10m_max"),
CloudCover          = GetFirstDouble(daily, "cloud_cover_mean"),
SunshineHours       = GetFirstDouble(daily, "sunshine_duration") / 3600.0,
ShortwaveRadiation  = GetFirstDouble(daily, "shortwave_radiation_sum"),
```

Add WMO→emoji helper (used in Task 3):
```csharp
public static string WmoToEmoji(int code) => code switch
{
    0            => "☀️",
    1            => "🌤️",
    2            => "⛅",
    3            => "☁️",
    45 or 48     => "🌫️",
    51 or 53 or 55 => "🌦️",
    56 or 57     => "🌨️",
    61 or 63 or 65 => "🌧️",
    66 or 67     => "🌨️",
    71 or 73 or 75 => "❄️",
    77           => "🌨️",
    80 or 81 or 82 => "🌧️",
    85 or 86     => "🌨️",
    95           => "⛈️",
    96 or 99     => "⛈️",
    _            => "🌡️"
};
```

Store the WMO code in `DayWeather` so the VM can call `WmoToEmoji`:
```csharp
public int WmoCode { get; set; }
```

**MainViewModel** — add computed string properties:
```csharp
public string WeatherConditionEmoji  => HasWeather ? WeatherService.WmoToEmoji(_dayWeather!.WmoCode) : "";
public string WeatherWindGust        => HasWeather ? $"{_dayWeather!.WindGust:F0} km/h" : "";
public string WeatherCloudCover      => HasWeather ? $"{_dayWeather!.CloudCover:F0}%" : "";
public string WeatherSunshineHours   => HasWeather ? $"{_dayWeather!.SunshineHours:F1} hrs" : "";
public string WeatherRadiation       => HasWeather ? $"{_dayWeather!.ShortwaveRadiation:F1} MJ/m²" : "";
```

Fire `OnPropertyChanged` for all new properties in the `DayWeather` setter.

**Verify:** Select today's date. Weather panel shows gust speed alongside max wind. Cloud cover %, sunshine hours, and radiation all display values. Condition emoji appears (☀️/⛅/🌧️ etc.).

---

### Task 3: Weather panel UI redesign (emoji+text, 2-line layout, larger font)

**Files:** `GardenDiary/MainWindow.xaml`

Replace the current single `<WrapPanel>` with a `<StackPanel>` containing two `<WrapPanel>` rows. Use `FontSize="13"` throughout (up from implied default ~11-12).

**Line 1:** Condition emoji+text · 🌡️ Temp · 💨 Wind (max + gust) · 🌧️ Rain

**Line 2:** ☁️ Cloud cover · ☀️ Sunshine hrs · ⚡ Solar radiation · 🌅 Sunrise · 🌇 Sunset

Each item: label `TextBlock` (emoji + text, `Foreground="#555"`, `Margin="0,0,3,0"`) + value `TextBlock` (`Margin="0,0,14,0"`).

Condition gets special treatment — emoji + description combined, `FontWeight="SemiBold"`, displayed first with larger margin.

```xml
<StackPanel>
    <!-- Row 1 -->
    <WrapPanel Margin="0,0,0,4">
        <TextBlock Text="{Binding WeatherConditionEmoji}" FontSize="16" Margin="0,0,4,0"/>
        <TextBlock Text="{Binding WeatherCondition}" FontSize="13" FontWeight="SemiBold" Margin="0,0,16,0"/>
        <TextBlock Text="🌡️ Temp:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherTempRange}" FontSize="13" Margin="0,0,14,0"/>
        <TextBlock Text="💨 Wind:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherWind}" FontSize="13" Margin="0,0,6,0"/>
        <TextBlock Text="(gust" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherWindGust}" FontSize="13" Margin="0,0,3,0"/>
        <TextBlock Text=")" FontSize="13" Foreground="#555" Margin="0,0,14,0"/>
        <TextBlock Text="🌧️ Rain:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherPrecipitation}" FontSize="13"/>
    </WrapPanel>
    <!-- Row 2 -->
    <WrapPanel>
        <TextBlock Text="☁️ Cloud:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherCloudCover}" FontSize="13" Margin="0,0,14,0"/>
        <TextBlock Text="☀️ Sun:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherSunshineHours}" FontSize="13" Margin="0,0,14,0"/>
        <TextBlock Text="⚡ Solar:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherRadiation}" FontSize="13" Margin="0,0,14,0"/>
        <TextBlock Text="🌅 Sunrise:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherSunrise}" FontSize="13" Margin="0,0,14,0"/>
        <TextBlock Text="🌇 Sunset:" FontSize="13" Foreground="#555" Margin="0,0,3,0"/>
        <TextBlock Text="{Binding WeatherSunset}" FontSize="13"/>
    </WrapPanel>
</StackPanel>
```

Note: Since the project already uses `Emoji.Wpf`, emoji in `<TextBlock>` will render correctly.

**Verify:** Weather panel shows two rows. Emoji render correctly. Condition has its emoji + text. Wind shows max and gust. Row 2 shows cloud/sunshine/radiation/sunrise/sunset. All text at FontSize 13. Panel stays compact — wraps gracefully if window is narrow.

---
