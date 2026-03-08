using System.IO;
using GardenDiary.Models;
using GardenDiary.Services;
using Xunit;

namespace GardenDiary.Tests;

public class DataServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataService _sut;

    public DataServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _sut = new DataService(_tempDir);
    }

    [Fact]
    public void LoadPlants_WhenNoFileExists_ReturnsEmptyList()
    {
        var result = _sut.LoadPlants();

        Assert.Empty(result);
    }

    [Fact]
    public void SaveAndLoad_PreservesPlantProperties()
    {
        var plant = new Plant
        {
            CommonName = "Tomato",
            LatinName  = "Solanum lycopersicum",
            Variety    = "Cherry Red"
        };

        _sut.SavePlants([plant]);
        var loaded = _sut.LoadPlants();

        Assert.Single(loaded);
        Assert.Equal("Tomato",                 loaded[0].CommonName);
        Assert.Equal("Solanum lycopersicum",   loaded[0].LatinName);
        Assert.Equal("Cherry Red",             loaded[0].Variety);
        Assert.Equal(plant.Id,                 loaded[0].Id);
    }

    [Fact]
    public void SaveAndLoad_PreservesDiaryEntries()
    {
        var entry = new DiaryEntry
        {
            Date        = new DateTime(2026, 3, 8),
            Watering    = true,
            Fertilizing = true,
            Notes       = "Fertilised with liquid feed"
        };
        var plant = new Plant { CommonName = "Rose", DiaryEntries = [entry] };

        _sut.SavePlants([plant]);
        var loaded = _sut.LoadPlants();

        var loadedEntry = Assert.Single(loaded[0].DiaryEntries);
        Assert.Equal(new DateTime(2026, 3, 8), loadedEntry.Date);
        Assert.True(loadedEntry.Watering);
        Assert.True(loadedEntry.Fertilizing);
        Assert.False(loadedEntry.Planting);
        Assert.Equal("Fertilised with liquid feed", loadedEntry.Notes);
    }

    [Fact]
    public void SavePlants_MultiplePlants_AllPreserved()
    {
        var plants = new List<Plant>
        {
            new() { CommonName = "Basil"  },
            new() { CommonName = "Mint"   },
            new() { CommonName = "Thyme"  }
        };

        _sut.SavePlants(plants);
        var loaded = _sut.LoadPlants();

        Assert.Equal(3, loaded.Count);
        Assert.Contains(loaded, p => p.CommonName == "Basil");
        Assert.Contains(loaded, p => p.CommonName == "Mint");
        Assert.Contains(loaded, p => p.CommonName == "Thyme");
    }

    [Fact]
    public void SavePlants_OverwritesPreviousSave()
    {
        _sut.SavePlants([new Plant { CommonName = "Old Plant" }]);
        _sut.SavePlants([new Plant { CommonName = "New Plant" }]);
        var loaded = _sut.LoadPlants();

        Assert.Single(loaded);
        Assert.Equal("New Plant", loaded[0].CommonName);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
