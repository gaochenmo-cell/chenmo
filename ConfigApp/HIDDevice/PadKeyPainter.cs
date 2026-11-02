using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// SVG-style corner arrows: thick uniform strokes forming diagonal + orthogonal L head
    /// (same geometry as the reference SVG / tukuppt L-arrow).
    /// </summary>
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
            inner.Inflate(-Math.Max(5, bounds.Width / 12), -Math.Max(5, bounds.Height / 12));

            if (kind == PadKeyKind.Center)
            {
                DrawCenterRing(g, inner, args);
                return;
            }

            using (GraphicsPath path = BuildSvgLArrowPath(inner, kind))
            {
                if (args.Triggered)
                {
                    using (SolidBrush fill = new SolidBrush(args.TriggerFill))
                        g.FillPath(fill, path);
                }

                float stroke = Math.Max(2f, Math.Min(inner.Width, inner.Height) * 0.035f);
                using (Pen outline = new Pen(args.OutlineColor, stroke))
                {
                    outline.LineJoin = LineJoin.Miter;
                    outline.MiterLimit = 8f;
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
                pen.StartCap = LineCap.Flat;
                pen.EndCap = LineCap.Flat;
                g.DrawEllipse(pen, ring);
            }
        }

        /// <summary>
        /// Single filled path matching SVG L-arrow: equal-thickness diagonal shaft + L head,
        /// built in unit space for down-right then mirrored. Junction is continuous (no gap).
        /// </summary>
        private static GraphicsPath BuildSvgLArrowPath(Rectangle inner, PadKeyKind kind)
        {
            // Unit geometry (0..1): thickness t, tip at bottom-right outer corner.
            const float t = 0.145f;
            float outer = 1f;
            float innerC = 1f - t;

            // Continuous outline of the union of:
            //  - horizontal bar [0.32, 1-t]..[1, 1]
            //  - vertical bar   [1-t, 0.32]..[1, 1]
            //  - diagonal shaft from upper-left into the inner corner (overlapping by t)
            GraphicsPath unit = new GraphicsPath(FillMode.Winding);

            PointF[] poly = new PointF[]
            {
                // start at outer tip corner, walk around clockwise
                new PointF(outer, outer),
                new PointF(0.32f, outer),
                new PointF(0.32f, innerC),
                // meet diagonal lower edge
                new PointF(innerC - t * 0.15f, innerC),
                new PointF(0.12f, 0.12f + t * 1.414f * 0.5f + t * 0.15f),
                new PointF(0.12f + t * 1.414f * 0.5f + t * 0.15f, 0.12f),
                new PointF(innerC, innerC - t * 0.15f),
                new PointF(innerC, 0.32f),
                new PointF(outer, 0.32f),
            };
            unit.AddPolygon(poly);

            using (Matrix m = CornerTransform(inner, kind))
            {
                GraphicsPath path = (GraphicsPath)unit.Clone();
                path.Transform(m);
                return path;
            }
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
