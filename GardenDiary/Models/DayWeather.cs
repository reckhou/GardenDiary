namespace GardenDiary.Models;

public class DayWeather
{
    public bool IsAvailable { get; set; }
    public string Condition { get; set; } = "";
    public double TempMax { get; set; }
    public double TempMin { get; set; }
    public double WindSpeed { get; set; }   // km/h
    public int WindDirection { get; set; }  // degrees
    public double Precipitation { get; set; } // mm
    public TimeOnly Sunrise { get; set; }
    public TimeOnly Sunset { get; set; }
    public string Error { get; set; } = "";
}
