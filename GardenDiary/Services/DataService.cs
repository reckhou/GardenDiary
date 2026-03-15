using System.IO;
using System.Text.Json;
using GardenDiary.Models;

namespace GardenDiary.Services;

public class DataService
{
    public string AppDataDir { get; }
    public string DataFilePath { get; }
    public string AreasFilePath { get; }
    public string TasksFilePath { get; }

    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public DataService() : this(null) { }

    public DataService(string? customDir)
    {
        AppDataDir = customDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GardenDiary");
        Directory.CreateDirectory(AppDataDir);
        DataFilePath  = Path.Combine(AppDataDir, "data.json");
        AreasFilePath = Path.Combine(AppDataDir, "areas.json");
        TasksFilePath = Path.Combine(AppDataDir, "tasks.json");
    }

    public List<Plant> LoadPlants()
    {
        if (!File.Exists(DataFilePath)) return new List<Plant>();
        var json = File.ReadAllText(DataFilePath);
        try { return JsonSerializer.Deserialize<List<Plant>>(json, _options) ?? new List<Plant>(); }
        catch (JsonException ex)
            { throw new InvalidDataException($"Plants data file is corrupted: {ex.Message}", ex); }
    }

    public void SavePlants(IEnumerable<Plant> plants)
    {
        var json = JsonSerializer.Serialize(plants.ToList(), _options);
        File.WriteAllText(DataFilePath, json);
    }

    public List<GardenArea> LoadAreas()
    {
        if (!File.Exists(AreasFilePath)) return new List<GardenArea>();
        var json = File.ReadAllText(AreasFilePath);
        try { return JsonSerializer.Deserialize<List<GardenArea>>(json, _options) ?? new List<GardenArea>(); }
        catch (JsonException ex)
            { throw new InvalidDataException($"Areas data file is corrupted: {ex.Message}", ex); }
    }

    public void SaveAreas(IEnumerable<GardenArea> areas)
    {
        var json = JsonSerializer.Serialize(areas.ToList(), _options);
        File.WriteAllText(AreasFilePath, json);
    }

    public List<GardenTask> LoadTasks()
    {
        if (!File.Exists(TasksFilePath)) return new List<GardenTask>();
        var json = File.ReadAllText(TasksFilePath);
        try { return JsonSerializer.Deserialize<List<GardenTask>>(json, _options) ?? new List<GardenTask>(); }
        catch (JsonException ex)
            { throw new InvalidDataException($"Tasks data file is corrupted: {ex.Message}", ex); }
    }

    public void SaveTasks(IEnumerable<GardenTask> tasks)
    {
        var json = JsonSerializer.Serialize(tasks.ToList(), _options);
        File.WriteAllText(TasksFilePath, json);
    }
}
