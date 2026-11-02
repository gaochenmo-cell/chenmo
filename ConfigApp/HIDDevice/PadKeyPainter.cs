using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>Vector pad key glyphs — L-corner arrows, outline style, trigger fill from LED color.</summary>
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

            using (GraphicsPath path = BuildCornerLArrow(inner, kind))
            {
                if (args.Triggered)
                {
                    using (SolidBrush fill = new SolidBrush(args.TriggerFill))
                        g.FillPath(fill, path);
                }
                float penW = Math.Max(2.2f, Math.Min(inner.Width, inner.Height) * 0.055f);
                using (Pen outline = new Pen(args.OutlineColor, penW))
                {
                    outline.LineJoin = LineJoin.Miter;
                    outline.MiterLimit = 4f;
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

        private static GraphicsPath BuildCornerLArrow(Rectangle inner, PadKeyKind kind)
        {
            GraphicsPath unit = BuildDownRightArrowUnit();
            using (Matrix m = CornerTransform(inner, kind))
            {
                GraphicsPath path = (GraphicsPath)unit.Clone();
                path.Transform(m);
                return path;
            }
        }

        /// <summary>Single winding path: L-head + diagonal shaft + junction patch (no gap).</summary>
        private static GraphicsPath BuildDownRightArrowUnit()
        {
            const float t = 0.13f;
            float ic = 1f - t;

            GraphicsPath path = new GraphicsPath(FillMode.Winding);

            path.AddRectangle(new RectangleF(0.34f, ic, 0.66f, t));
            path.AddRectangle(new RectangleF(ic, 0.34f, t, ic - 0.34f));

            PointF a = new PointF(0.10f, 0.28f);
            PointF b = new PointF(0.10f + t, 0.28f + t);
            PointF c = new PointF(ic + t * 0.08f, ic + t * 0.08f);
            PointF d = new PointF(ic - t, ic - t);
            path.AddPolygon(new[] { a, b, c, d });

            path.AddRectangle(new RectangleF(ic - t, ic - t, t * 1.1f, t * 1.1f));
            path.AddRectangle(new RectangleF(0.34f - t * 0.05f, ic - t * 0.05f, t * 1.1f, t * 1.1f));

            return path;
        }

        private static Matrix CornerTransform(Rectangle inner, PadKeyKind kind)
        {
            float cx = inner.X + inner.Width * 0.5f;
            float cy = inner.Y + inner.Height * 0.5f;
            Matrix m = new Matrix();
            m.Translate(-0.5f, -0.5f, MatrixOrder.Append);
            switch (kind)
            {
                case PadKeyKind.DownRight:
                    break;
                case PadKeyKind.UpperLeft:
                    m.Scale(-1f, -1f, MatrixOrder.Append);
                    break;
                case PadKeyKind.UpperRight:
                    m.Scale(1f, -1f, MatrixOrder.Append);
                    break;
                case PadKeyKind.DownLeft:
                    m.Scale(-1f, 1f, MatrixOrder.Append);
                    break;
            }
            m.Scale(inner.Width, inner.Height, MatrixOrder.Append);
            m.Translate(cx, cy, MatrixOrder.Append);
            return m;
        }
    }
}
