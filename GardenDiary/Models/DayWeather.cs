namespace GardenDiary.Models;

public class DayWeather
{
    public bool IsAvailable { get; set; }
    public int WmoCode { get; set; }
    public string Condition { get; set; } = "";
    public double TempMax { get; set; }
    public double TempMin { get; set; }
    public double WindSpeed { get; set; }    // km/h max
    public double WindGust { get; set; }     // km/h max gust
    public int WindDirection { get; set; }   // degrees
    public double Precipitation { get; set; } // mm
    public double CloudCover { get; set; }   // % mean
    public double SunshineHours { get; set; } // hours
    public double ShortwaveRadiation { get; set; } // MJ/m²
    public TimeOnly Sunrise { get; set; }
    public TimeOnly Sunset { get; set; }
    public string Error { get; set; } = "";
}
