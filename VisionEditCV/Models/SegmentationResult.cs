namespace VisionEditCV.Models
{
    /// <summary>
    /// Holds the result of a SAM3 segmentation request.
    /// Masks are stored as float[H, W] arrays (values 0..1).
    /// Boxes are [x, y, w, h] in image-space float coordinates.
    /// </summary>
    public class SegmentationResult
    {
        /// <summary>N masks, each [H, W] float array (probability 0..1)</summary>
        public List<float[,]> Masks { get; set; } = new();

        /// <summary>N bounding boxes [x, y, w, h]</summary>
        public List<float[]> Boxes { get; set; } = new();

        /// <summary>N confidence scores</summary>
        public List<float> Scores { get; set; } = new();
    }
}
