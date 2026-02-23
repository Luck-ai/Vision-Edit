namespace VisionEditCV
{
    /// <summary>
    /// Extension helpers for <see cref="Graphics"/> used by custom controls.
    /// </summary>
    internal static class GraphicsExtensions
    {
        /// <summary>Fills a rectangle with rounded corners using the supplied brush.</summary>
        public static void FillRoundedRect(
            this Graphics g, Brush brush, RectangleF rect, float radius)
        {
            float d = radius * 2f;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X,          rect.Y,           d, d, 180, 90);
            path.AddArc(rect.Right - d,  rect.Y,           d, d, 270, 90);
            path.AddArc(rect.Right - d,  rect.Bottom - d,  d, d, 0,   90);
            path.AddArc(rect.X,          rect.Bottom - d,  d, d, 90,  90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        /// <summary>Draws the outline of a rectangle with rounded corners.</summary>
        public static void DrawRoundedRect(
            this Graphics g, Pen pen, RectangleF rect, float radius)
        {
            float d = radius * 2f;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X,          rect.Y,           d, d, 180, 90);
            path.AddArc(rect.Right - d,  rect.Y,           d, d, 270, 90);
            path.AddArc(rect.Right - d,  rect.Bottom - d,  d, d, 0,   90);
            path.AddArc(rect.X,          rect.Bottom - d,  d, d, 90,  90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}
