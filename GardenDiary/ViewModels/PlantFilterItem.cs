namespace GardenDiary.ViewModels;

public class PlantFilterItem
{
    public string Label  { get; }
    /// <summary>null = All, Guid.Empty = Not Placed, specific Guid = area</summary>
    public Guid?  AreaId { get; }

    public PlantFilterItem(string label, Guid? areaId)
    {
        Label  = label;
        AreaId = areaId;
    }
}
