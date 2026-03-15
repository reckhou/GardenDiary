using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using GardenDiary.Models;

namespace GardenDiary.Services;

public class WeatherService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<DayWeather> GetWeatherAsync(double lat, double lon, DateOnly date)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var daysFromToday = (date.ToDateTime(TimeOnly.MinValue) - DateTime.Today).TotalDays;
            // Use forecast API for recent/future dates only; archive API has reliable
            // complete data for anything older than ~7 days. The forecast API's rolling
            // 92-day window drops data at the edges, causing nulls for weather fields.
            var baseUrl = daysFromToday >= -7
                ? "https://api.open-meteo.com/v1/forecast"
                : "https://archive-api.open-meteo.com/v1/archive";

            var url = $"{baseUrl}?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                      $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                      "&daily=sunrise,sunset,precipitation_sum,wind_speed_10m_max," +
                      "wind_direction_10m_dominant,weather_code,temperature_2m_max,temperature_2m_min" +
                      $"&timezone=auto&start_date={dateStr}&end_date={dateStr}";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");

            return new DayWeather
            {
                IsAvailable   = true,
                TempMax       = GetFirstDouble(daily, "temperature_2m_max"),
                TempMin       = GetFirstDouble(daily, "temperature_2m_min"),
                WindSpeed     = GetFirstDouble(daily, "wind_speed_10m_max"),
                WindDirection = (int)GetFirstDouble(daily, "wind_direction_10m_dominant"),
                Precipitation = GetFirstDouble(daily, "precipitation_sum"),
                Condition     = WmoToDescription((int)GetFirstDouble(daily, "weather_code")),
                Sunrise       = ParseTimeOnly(GetFirstString(daily, "sunrise")),
                Sunset        = ParseTimeOnly(GetFirstString(daily, "sunset")),
            };
        }
        catch (Exception ex)
        {
            return new DayWeather { IsAvailable = false, Error = ex.Message };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double GetFirstDouble(JsonElement daily, string key)
    {
        if (!daily.TryGetProperty(key, out var prop)) return 0;
        if (prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() == 0) return 0;
        var el = prop[0];
        return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : 0;
    }

    private static string GetFirstString(JsonElement daily, string key)
    {
        if (!daily.TryGetProperty(key, out var prop)) return "";
        if (prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() == 0) return "";
        return prop[0].GetString() ?? "";
    }

    private static TimeOnly ParseTimeOnly(string iso)
    {
        // Open-Meteo returns "2024-01-15T07:23" in local timezone
        return DateTime.TryParse(iso, out var dt) ? TimeOnly.FromDateTime(dt) : default;
    }

    public static string DegreesToCompass(int degrees)
    {
        string[] dirs = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        return dirs[(int)((degrees + 22.5) / 45) % 8];
    }

    private static string WmoToDescription(int code) => code switch
    {
        0            => "Clear sky",
        1            => "Mainly clear",
        2            => "Partly cloudy",
        3            => "Overcast",
        45 or 48     => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57     => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67     => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77           => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86     => "Snow showers",
        95           => "Thunderstorm",
        96 or 99     => "Thunderstorm with hail",
        _            => $"Code {code}"
    };
}
