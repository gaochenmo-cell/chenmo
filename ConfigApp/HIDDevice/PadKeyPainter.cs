using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>Vector pad key glyphs (anti-aliased) for Yuanpad 5Key layout.</summary>
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
            {
                g.FillRectangle(backBrush, bounds);
            }

            Rectangle inner = bounds;
            inner.Inflate(-Math.Max(4, bounds.Width / 12), -Math.Max(4, bounds.Height / 12));

            if (kind == PadKeyKind.Center)
            {
                DrawCenterRing(g, inner, highlighted);
                return;
            }

            Color arrowFill = highlighted ? Color.FromArgb(255, 255, 120, 220) : Color.FromArgb(255, 210, 0, 150);
            using (GraphicsPath path = BuildArrowPath(inner, kind))
            using (SolidBrush fill = new SolidBrush(arrowFill))
            {
                g.FillPath(fill, path);
            }
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

        private static GraphicsPath BuildArrowPath(Rectangle inner, PadKeyKind kind)
        {
            float cx = inner.X + inner.Width * 0.5f;
            float cy = inner.Y + inner.Height * 0.5f;
            float arm = Math.Min(inner.Width, inner.Height) * 0.38f;
            float head = arm * 0.55f;
            float shaft = arm * 0.22f;

            PointF tip;
            PointF tail;
            switch (kind)
            {
                case PadKeyKind.UpperLeft:
                    tip = new PointF(inner.Left + inner.Width * 0.18f, inner.Top + inner.Height * 0.18f);
                    tail = new PointF(cx, cy);
                    break;
                case PadKeyKind.UpperRight:
                    tip = new PointF(inner.Right - inner.Width * 0.18f, inner.Top + inner.Height * 0.18f);
                    tail = new PointF(cx, cy);
                    break;
                case PadKeyKind.DownLeft:
                    tip = new PointF(inner.Left + inner.Width * 0.18f, inner.Bottom - inner.Height * 0.18f);
                    tail = new PointF(cx, cy);
                    break;
                default: // DownRight
                    tip = new PointF(inner.Right - inner.Width * 0.18f, inner.Bottom - inner.Height * 0.18f);
                    tail = new PointF(cx, cy);
                    break;
            }

            float dx = tip.X - tail.X;
            float dy = tip.Y - tail.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1f)
                len = 1f;
            dx /= len;
            dy /= len;
            float px = -dy;
            float py = dx;

            PointF baseCenter = new PointF(tip.X - dx * head, tip.Y - dy * head);
            PointF s1 = new PointF(tail.X + px * shaft, tail.Y + py * shaft);
            PointF s2 = new PointF(tail.X - px * shaft, tail.Y - py * shaft);
            PointF b1 = new PointF(baseCenter.X + px * head * 0.45f, baseCenter.Y + py * head * 0.45f);
            PointF b2 = new PointF(baseCenter.X - px * head * 0.45f, baseCenter.Y - py * head * 0.45f);

            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(new[] { tip, b1, s1, s2, b2 });
            path.CloseFigure();
            return path;
        }
    }
}
