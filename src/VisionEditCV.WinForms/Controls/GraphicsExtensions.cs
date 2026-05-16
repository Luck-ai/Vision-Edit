namespace VisionEditCV
{
    internal static class GraphicsExtensions
    {
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
