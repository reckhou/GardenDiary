namespace GardenDiary.Models;

public enum ShapeType
{
    Rectangle,
    Circle
}

public class GardenShape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ShapeType Type { get; set; } = ShapeType.Rectangle;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 80;
    public double Height { get; set; } = 60;
    public string FillColor { get; set; } = "#90CAF9";
    public double Opacity { get; set; } = 0.4;
    public string Label { get; set; } = "";
    public int ZIndex { get; set; }
}
