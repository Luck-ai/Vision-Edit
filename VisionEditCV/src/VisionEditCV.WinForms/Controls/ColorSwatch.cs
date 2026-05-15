namespace VisionEditCV
{
    [System.ComponentModel.DesignerCategory("Component")]
    public class ColorSwatch : Control
    {
        private Color _color = Color.White;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(typeof(Color), "White")]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public Color SelectedColor
        {
            get => _color;
            set { _color = value; Invalidate(); }
        }

        public event EventHandler? ColorChanged;

        public ColorSwatch()
        {
            SetStyle(ControlStyles.UserPaint        |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size   = new Size(36, 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(_color);
            using var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnClick(EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _color, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _color = dlg.Color;
                Invalidate();
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
