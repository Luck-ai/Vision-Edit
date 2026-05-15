namespace VisionEditCV.Models
{
    public class SegmentationResult
    {
        public List<float[,]> Masks { get; set; } = new();

        public List<float[]> Boxes { get; set; } = new();

        public List<float> Scores { get; set; } = new();
    }
}
