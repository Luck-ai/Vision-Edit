using Avalonia;

namespace VisionEditCV.Desktop.Models;

public class BBoxEntry
{
    public Rect Rect { get; set; }
    public bool Label { get; set; } = true; // true = FG, false = BG

    public BBoxEntry(Rect rect, bool label = true)
    {
        Rect = rect;
        Label = label;
    }
}
