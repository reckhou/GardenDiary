using System.IO;
using System.Text.Json;
using GardenDiary.Models;

namespace GardenDiary.Services;

public class DataService
{
    public string AppDataDir { get; }
    public string DataFilePath { get; }

    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public DataService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataDir = Path.Combine(appData, "GardenDiary");
        Directory.CreateDirectory(AppDataDir);
        DataFilePath = Path.Combine(AppDataDir, "data.json");
    }

    public List<Plant> LoadPlants()
    {
        if (!File.Exists(DataFilePath)) return new List<Plant>();
        var json = File.ReadAllText(DataFilePath);
        return JsonSerializer.Deserialize<List<Plant>>(json, _options) ?? new List<Plant>();
    }

    public void SavePlants(IEnumerable<Plant> plants)
    {
        var json = JsonSerializer.Serialize(plants.ToList(), _options);
        File.WriteAllText(DataFilePath, json);
    }
}
