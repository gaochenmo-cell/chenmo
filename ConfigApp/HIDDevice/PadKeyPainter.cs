using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>Vector pad key glyphs — L-corner arrows matching classic PIU pad art.</summary>
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

        internal static void WireButton(Button btn, PadKeyKind kind)
        {
            btn.Tag = kind;
            btn.BackgroundImage = null;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(160, 160, 160);
            btn.Text = string.Empty;
            btn.UseVisualStyleBackColor = true;
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

        internal static void PaintKey(Graphics g, Rectangle bounds, PadKeyKind kind, bool highlighted)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            Color back = highlighted ? Color.IndianRed : SystemColors.ButtonFace;
            using (SolidBrush backBrush = new SolidBrush(back))
                g.FillRectangle(backBrush, bounds);

            Rectangle inner = bounds;
            inner.Inflate(-Math.Max(4, bounds.Width / 14), -Math.Max(4, bounds.Height / 14));

            if (kind == PadKeyKind.Center)
            {
                DrawCenterRing(g, inner, highlighted);
                return;
            }

            Color arrowFill = highlighted ? Color.FromArgb(255, 255, 180, 230) : Color.FromArgb(255, 210, 0, 150);
            using (GraphicsPath path = BuildCornerLArrow(inner, kind))
            using (SolidBrush fill = new SolidBrush(arrowFill))
                g.FillPath(fill, path);
        }

        private static void PadKeyButton_Paint(object sender, PaintEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || !(btn.Tag is PadKeyKind))
                return;

            App_DDR form = btn.FindForm() as App_DDR;
            bool highlighted = form != null && form.IsPadKeyHighlighted((PadKeyKind)btn.Tag);

            e.Graphics.Clear(btn.BackColor);
            PaintKey(e.Graphics, btn.ClientRectangle, (PadKeyKind)btn.Tag, highlighted);
        }

        private static void DrawCenterRing(Graphics g, Rectangle inner, bool highlighted)
        {
            float stroke = Math.Max(3f, inner.Width * 0.08f);
            Color ring = highlighted ? Color.FromArgb(255, 255, 240, 80) : Color.FromArgb(255, 255, 200, 0);
            using (Pen pen = new Pen(ring, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float pad = stroke * 0.6f;
                g.DrawEllipse(pen, inner.X + pad, inner.Y + pad, inner.Width - pad * 2, inner.Height - pad * 2);
            }
        }

        /// <summary>
        /// Classic thick diagonal shaft + orthogonal L head (reference: corner arrow pointing to cell corner).
        /// Built once in unit square for down-right, then mirrored for other corners.
        /// </summary>
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

        private static GraphicsPath BuildDownRightArrowUnit()
        {
            // Coordinates 0..1; arrow points to bottom-right outer corner.
            const float t = 0.13f;

            GraphicsPath path = new GraphicsPath(FillMode.Winding);

            // Bottom horizontal leg of L
            path.AddRectangle(new RectangleF(0.34f, 1f - t, 0.66f, t));
            // Right vertical leg of L
            path.AddRectangle(new RectangleF(1f - t, 0.34f, t, 0.66f));

            // Diagonal shaft (45° bar from upper-left toward inner corner of L)
            PointF a = new PointF(0.10f, 0.30f);
            PointF b = new PointF(0.10f + t, 0.30f);
            PointF c = new PointF(1f - t, 1f - t - t * 0.15f);
            PointF d = new PointF(1f - t - t * 0.15f, 1f - t);
            path.AddPolygon(new[] { a, b, c, d });

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
