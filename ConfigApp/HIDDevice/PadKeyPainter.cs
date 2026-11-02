using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>Vector pad key glyphs — chevron arrows, outline style, trigger fill from LED color.</summary>
    internal static class PadKeyPainter
    {
        internal enum PadKeyKind
        {
            UpperLeft = 0,
            UpperRight = 1,
            Center = 2,
            DownLeft = 3,
            DownRight = 4
        }

        internal struct PadKeyPaintArgs
        {
            public Color CellBackground;
            public Color OutlineColor;
            public bool Selected;
            public bool Triggered;
            public Color TriggerFill;
        }

        internal static void WireButton(Button btn, PadKeyKind kind)
        {
            btn.Tag = kind;
            btn.BackgroundImage = null;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(160, 160, 160);
            btn.Text = string.Empty;
            btn.UseVisualStyleBackColor = false;
            EnableUserPaint(btn);
            btn.Paint -= PadKeyButton_Paint;
            btn.Paint += PadKeyButton_Paint;
        }

        private static void EnableUserPaint(Control ctrl)
        {
            ControlStyles styles = ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw;
            MethodInfo setStyle = typeof(Control).GetMethod(
                "SetStyle",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            if (setStyle != null)
                setStyle.Invoke(ctrl, new object[] { styles, true });
        }

        internal static void PaintKey(Graphics g, Rectangle bounds, PadKeyKind kind, PadKeyPaintArgs args)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            using (SolidBrush backBrush = new SolidBrush(args.CellBackground))
                g.FillRectangle(backBrush, bounds);

            if (args.Selected)
            {
                using (Pen sel = new Pen(Color.FromArgb(120, 100, 149, 237), Math.Max(2f, bounds.Width * 0.02f)))
                    g.DrawRectangle(sel, 1, 1, bounds.Width - 2, bounds.Height - 2);
            }

            Rectangle inner = bounds;
            inner.Inflate(-Math.Max(4, bounds.Width / 14), -Math.Max(4, bounds.Height / 14));

            if (kind == PadKeyKind.Center)
            {
                DrawCenterRing(g, inner, args);
                return;
            }

            using (GraphicsPath path = BuildChevronArrow(inner, kind))
            {
                if (args.Triggered)
                {
                    using (SolidBrush fill = new SolidBrush(args.TriggerFill))
                        g.FillPath(fill, path);
                }
                float penW = Math.Max(2f, Math.Min(inner.Width, inner.Height) * 0.04f);
                using (Pen outline = new Pen(args.OutlineColor, penW))
                {
                    outline.LineJoin = LineJoin.Miter;
                    outline.MiterLimit = 5f;
                    outline.StartCap = LineCap.Flat;
                    outline.EndCap = LineCap.Flat;
                    g.DrawPath(outline, path);
                }
            }
        }

        private static void PadKeyButton_Paint(object sender, PaintEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || !(btn.Tag is PadKeyKind))
                return;

            App_DDR form = btn.FindForm() as App_DDR;
            PadKeyPaintArgs args = form != null
                ? form.GetPadKeyPaintArgs((PadKeyKind)btn.Tag)
                : default(PadKeyPaintArgs);

            if (form == null)
            {
                args.CellBackground = SystemColors.ButtonFace;
                args.OutlineColor = Color.Black;
            }

            e.Graphics.Clear(args.CellBackground);
            PaintKey(e.Graphics, btn.ClientRectangle, (PadKeyKind)btn.Tag, args);
        }

        private static void DrawCenterRing(Graphics g, Rectangle inner, PadKeyPaintArgs args)
        {
            float stroke = Math.Max(3f, inner.Width * 0.08f);
            float pad = stroke * 0.6f;
            RectangleF ring = new RectangleF(
                inner.X + pad, inner.Y + pad,
                inner.Width - pad * 2, inner.Height - pad * 2);

            if (args.Triggered)
            {
                using (SolidBrush fill = new SolidBrush(args.TriggerFill))
                    g.FillEllipse(fill, ring);
            }

            using (Pen pen = new Pen(args.OutlineColor, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawEllipse(pen, ring);
            }
        }

        /// <summary>Thick chevron (reference: simple &gt; shape) pointing toward each corner.</summary>
        private static GraphicsPath BuildChevronArrow(Rectangle inner, PadKeyKind kind)
        {
            float cx = inner.X + inner.Width * 0.5f;
            float cy = inner.Y + inner.Height * 0.5f;
            float scale = Math.Min(inner.Width, inner.Height) * 0.44f;
            float angle = ChevronAngleDegrees(kind);

            // Unit chevron pointing east (tip right), centered at origin
            PointF[] unit = new PointF[]
            {
                new PointF(0.82f, 0f),
                new PointF(0.08f, -0.22f),
                new PointF(-0.40f, -0.36f),
                new PointF(-0.40f, 0.36f),
                new PointF(0.08f, 0.22f),
            };

            PointF[] pts = new PointF[unit.Length];
            for (int i = 0; i < unit.Length; i++)
                pts[i] = new PointF(unit[i].X * scale, unit[i].Y * scale);

            using (Matrix m = new Matrix())
            {
                m.Rotate(angle);
                m.TransformPoints(pts);
            }

            for (int i = 0; i < pts.Length; i++)
            {
                pts[i].X += cx;
                pts[i].Y += cy;
            }

            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            path.AddPolygon(pts);
            return path;
        }

        private static float ChevronAngleDegrees(PadKeyKind kind)
        {
            switch (kind)
            {
                case PadKeyKind.UpperLeft: return 225f;
                case PadKeyKind.UpperRight: return -45f;
                case PadKeyKind.DownLeft: return 135f;
                case PadKeyKind.DownRight: return 45f;
                default: return 0f;
            }
        }
    }
}
