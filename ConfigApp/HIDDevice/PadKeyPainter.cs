using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Pad key glyphs: three stacked V-chevrons ("speed" arrow) in a cyan neon gradient,
    /// rotated towards the key's corner; the centre key is a matching neon ring.
    /// </summary>
    internal static class PadKeyPainter
    {
        internal enum PadKeyKind
        {
            // 5-key (PIU) layout: value == firmware key index
            UpperLeft = 0,
            UpperRight = 1,
            Center = 2,
            DownLeft = 3,
            DownRight = 4,
            // 4-key (DDR cross) layout: same chevron, axis-aligned; key index comes from App_DDR.FourKeyKinds
            Left = 5,
            Up = 6,
            Down = 7,
            Right = 8
        }

        internal struct PadKeyPaintArgs
        {
            public Color CellBackground;
            public Color OutlineColor;
            public bool Selected;
            public bool Triggered;
            public Color TriggerFill;
            public Color SelectedFill;
            public Color BorderColor;
            /// <summary>Second gradient stop for the selected cell; Color.Empty paints SelectedFill solid.</summary>
            public Color SelectedGradientEnd;
            /// <summary>Outer glow / neon stroke colour; Color.Empty disables every glow effect (light theme).</summary>
            public Color GlowColor;
            /// <summary>Chevron / ring gradient: front (tip) → middle → back layer, plus the halo colour.
            /// Empty values fall back to the built-in cyan set.</summary>
            public Color ArrowTip, ArrowMid, ArrowBack, ArrowGlow;
        }

        // Default (dark theme) cyan set: #22D3EE → #06B6D4 → #0E7490
        private static readonly Color DefaultTip = Color.FromArgb(0x22, 0xD3, 0xEE);
        private static readonly Color DefaultMid = Color.FromArgb(0x06, 0xB6, 0xD4);
        private static readonly Color DefaultBack = Color.FromArgb(0x0E, 0x74, 0x90);

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

            // Card: rounded cell, filled with the selection colour when selected, thin border otherwise.
            // Neon (dark) theme: the cell is inset a few px so an outer glow fits inside the button,
            // the selected cell gets a diagonal gradient and pressed cells glow in their LED colour.
            bool neon = !args.GlowColor.IsEmpty;
            bool selectedFill = args.Selected && !args.SelectedFill.IsEmpty;
            bool gradient = selectedFill && !args.SelectedGradientEnd.IsEmpty;
            int glowPad = neon ? Math.Max(3, bounds.Width / 40) : 0;
            Color cardFill = selectedFill ? args.SelectedFill : args.CellBackground;
            Rectangle card = new Rectangle(bounds.X + 1 + glowPad, bounds.Y + 1 + glowPad,
                bounds.Width - 3 - glowPad * 2, bounds.Height - 3 - glowPad * 2);
            using (GraphicsPath cardPath = RoundedRect(card, Math.Max(4, bounds.Width / 14)))
            {
                if (neon && (args.Selected || args.Triggered))
                    DrawOuterGlow(g, cardPath, args.Triggered ? Color.FromArgb(255, args.TriggerFill) : args.GlowColor, glowPad);

                if (gradient)
                {
                    using (LinearGradientBrush backBrush = new LinearGradientBrush(card, args.SelectedFill, args.SelectedGradientEnd, 45f))
                        g.FillPath(backBrush, cardPath);
                }
                else
                {
                    using (SolidBrush backBrush = new SolidBrush(cardFill))
                        g.FillPath(backBrush, cardPath);
                }

                Color border = args.BorderColor.IsEmpty ? Color.FromArgb(209, 213, 219) : args.BorderColor;
                if (selectedFill)
                    border = gradient ? args.GlowColor : args.SelectedFill;
                else if (neon && args.Triggered)
                    border = Color.FromArgb(255, args.TriggerFill);
                using (Pen borderPen = new Pen(border, 1f))
                    g.DrawPath(borderPen, cardPath);
            }

            Rectangle inner = bounds;
            inner.Inflate(-Math.Max(5, bounds.Width / 12), -Math.Max(5, bounds.Height / 12));

            if (kind == PadKeyKind.Center)
            {
                DrawCenterRing(g, inner, args);
                return;
            }

            DrawChevronStack(g, inner, kind, args);
        }

        /// <summary>Resolve the three gradient stops + halo for the current state: the LED colour
        /// while the pad is pressed (existing highlight rule), otherwise the theme's cyan set.</summary>
        private static void ResolveArrowColors(PadKeyPaintArgs args, out Color tip, out Color mid, out Color back, out Color glow)
        {
            if (args.Triggered)
            {
                Color led = Color.FromArgb(255, args.TriggerFill);
                tip = Mix(led, Color.White, 0.45f);
                mid = led;
                back = Mix(led, Color.Black, 0.35f);
                glow = led;
                return;
            }
            tip = args.ArrowTip.IsEmpty ? DefaultTip : args.ArrowTip;
            mid = args.ArrowMid.IsEmpty ? DefaultMid : args.ArrowMid;
            back = args.ArrowBack.IsEmpty ? DefaultBack : args.ArrowBack;
            glow = args.ArrowGlow.IsEmpty ? tip : args.ArrowGlow;
        }

        private static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(255,
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        /// <summary>
        /// Three V-chevrons stacked along the arrow direction (reference: cyan "speed" chevrons).
        /// Layer 0 is the front/tip layer: brightest, filled with the tip→mid gradient, outer glow
        /// and a bright edge. Layers 1–2 sit behind it, slightly smaller, darker and more translucent.
        /// </summary>
        private static void DrawChevronStack(Graphics g, Rectangle inner, PadKeyKind kind, PadKeyPaintArgs args)
        {
            Color tip, mid, back, glow;
            ResolveArrowColors(args, out tip, out mid, out back, out glow);

            float side = Math.Min(inner.Width, inner.Height);
            float stroke = Math.Max(1.5f, side * 0.03f);

            // Per-layer: tip position along the direction axis (unit space, 0 = key centre),
            // size factor, fill alpha, glow alpha. Back layers first so the front overlaps them.
            float[] tipU = { -0.10f, 0.17f, 0.46f };
            float[] scale = { 0.79f, 0.95f, 1.10f };
            // Less translucency on the coloured selected card so the back layers stay cyan
            int[] fillA = args.Selected ? new int[] { 215, 240, 255 } : new int[] { 150, 205, 255 };
            int[] glowA = { 0, 45, 95 };
            Color[] c0 = { back, mid, tip };
            Color[] c1 = { Mix(back, Color.Black, 0.25f), back, mid };

            using (Matrix m = ArrowTransform(inner, kind))
            {
                for (int i = 0; i < 3; i++)
                {
                    using (GraphicsPath path = BuildChevronPath(tipU[i], scale[i]))
                    {
                        path.Transform(m);

                        // Gradient runs from the chevron tip (bright) back to its base (darker)
                        PointF[] axis = { new PointF(tipU[i], 0f), new PointF(tipU[i] - 0.44f * scale[i], 0f) };   // 0.44 = d + t
                        m.TransformPoints(axis);

                        if (glowA[i] > 0)
                        {
                            using (Pen halo = new Pen(Alpha(glow, glowA[i] / 2), stroke * 5f))
                            {
                                halo.LineJoin = LineJoin.Round;
                                g.DrawPath(halo, path);
                            }
                            using (Pen halo = new Pen(Alpha(glow, glowA[i]), stroke * 2.4f))
                            {
                                halo.LineJoin = LineJoin.Round;
                                g.DrawPath(halo, path);
                            }
                        }

                        using (LinearGradientBrush fill = new LinearGradientBrush(axis[0], axis[1], Alpha(c0[i], fillA[i]), Alpha(c1[i], fillA[i])))
                            g.FillPath(fill, path);

                        // Crisp neon edge on the front layer (white on the selected card for contrast)
                        if (i == 2)
                        {
                            Color edge = args.Selected ? Color.White : Mix(tip, Color.White, 0.55f);
                            using (Pen edgePen = new Pen(Alpha(edge, 230), Math.Max(1f, stroke * 0.55f)))
                            {
                                edgePen.LineJoin = LineJoin.Round;
                                g.DrawPath(edgePen, path);
                            }
                        }
                        else
                        {
                            using (Pen edgePen = new Pen(Alpha(Mix(c0[i], Color.White, 0.3f), 120), Math.Max(1f, stroke * 0.4f)))
                            {
                                edgePen.LineJoin = LineJoin.Round;
                                g.DrawPath(edgePen, path);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// One V-chevron pointing along +x in unit space (key centre at the origin, key half-side = 0.5).
        /// tipU = x of the point; scale shrinks arm length, thickness and spread together.
        /// Arms open at 90° so the shape stays inside the cell once rotated 45° towards a corner.
        /// </summary>
        private static GraphicsPath BuildChevronPath(float tipU, float scale)
        {
            float h = 0.30f * scale;   // half spread of the arms
            float d = 0.30f * scale;   // depth from arm end to notch
            float t = 0.14f * scale;   // arm thickness along x
            float baseU = tipU - d - t;

            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            path.AddPolygon(new PointF[]
            {
                new PointF(baseU, -h),
                new PointF(baseU + t, -h),
                new PointF(tipU, 0f),
                new PointF(baseU + t, h),
                new PointF(baseU, h),
                new PointF(baseU + d, 0f),
            });
            return path;
        }

        /// <summary>Unit space → key pixels, rotated so +x points at the key's corner.</summary>
        private static Matrix ArrowTransform(Rectangle inner, PadKeyKind kind)
        {
            float side = Math.Min(inner.Width, inner.Height);
            float cx = inner.X + inner.Width * 0.5f;
            float cy = inner.Y + inner.Height * 0.5f;
            float angle;
            switch (kind)
            {
                case PadKeyKind.UpperLeft: angle = 225f; break;
                case PadKeyKind.UpperRight: angle = 315f; break;
                case PadKeyKind.DownLeft: angle = 135f; break;
                case PadKeyKind.Left: angle = 180f; break;
                case PadKeyKind.Up: angle = 270f; break;
                case PadKeyKind.Down: angle = 90f; break;
                case PadKeyKind.Right: angle = 0f; break;
                default: angle = 45f; break;   // DownRight
            }
            Matrix m = new Matrix();
            m.Rotate(angle, MatrixOrder.Append);
            m.Scale(side, side, MatrixOrder.Append);
            m.Translate(cx, cy, MatrixOrder.Append);
            return m;
        }

        internal static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width <= d || r.Height <= d)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

            Color behind = btn.Parent != null ? btn.Parent.BackColor : args.CellBackground;
            e.Graphics.Clear(behind);
            PaintKey(e.Graphics, btn.ClientRectangle, (PadKeyKind)btn.Tag, args);
        }

        /// <summary>Centre key: neon cyan ring in the same gradient as the chevrons — outer halo,
        /// gradient main ring, and a thinner darker inner ring echoing the layered arrows.</summary>
        private static void DrawCenterRing(Graphics g, Rectangle inner, PadKeyPaintArgs args)
        {
            Color tip, mid, back, glow;
            ResolveArrowColors(args, out tip, out mid, out back, out glow);

            float side = Math.Min(inner.Width, inner.Height);
            float stroke = Math.Max(3f, side * 0.075f);
            float pad = stroke * 1.3f;
            RectangleF ring = new RectangleF(
                inner.X + pad, inner.Y + pad,
                inner.Width - pad * 2, inner.Height - pad * 2);

            if (args.Triggered)
            {
                using (SolidBrush fill = new SolidBrush(args.TriggerFill))
                    g.FillEllipse(fill, ring);
            }

            using (Pen halo = new Pen(Alpha(glow, 45), stroke * 2.6f))
                g.DrawEllipse(halo, ring);
            using (Pen halo = new Pen(Alpha(glow, 90), stroke * 1.6f))
                g.DrawEllipse(halo, ring);

            using (LinearGradientBrush grad = new LinearGradientBrush(ring, tip, back, 45f))
            using (Pen pen = new Pen(grad, stroke))
                g.DrawEllipse(pen, ring);

            Color edge = args.Selected ? Color.White : Mix(tip, Color.White, 0.55f);
            using (Pen edgePen = new Pen(Alpha(edge, 220), Math.Max(1f, stroke * 0.18f)))
                g.DrawEllipse(edgePen, ring);

            float innerPad = stroke * 1.15f;
            RectangleF innerRing = new RectangleF(ring.X + innerPad, ring.Y + innerPad,
                ring.Width - innerPad * 2, ring.Height - innerPad * 2);
            if (innerRing.Width > stroke * 2)
            {
                using (Pen innerPen = new Pen(Alpha(Mix(mid, back, 0.5f), 170), Math.Max(1f, stroke * 0.35f)))
                    g.DrawEllipse(innerPen, innerRing);
            }
        }

        /// <summary>Soft outer glow: three concentric translucent strokes centred on the card edge,
        /// widest and faintest first. Half of each pen width lands outside the card, inside glowPad.</summary>
        private static void DrawOuterGlow(Graphics g, GraphicsPath cardPath, Color color, int glowPad)
        {
            const int passes = 3;
            for (int i = passes; i >= 1; i--)
            {
                float width = glowPad * 2f * i / passes;
                int alpha = 40 + (passes - i) * 35;
                using (Pen p = new Pen(Color.FromArgb(alpha, color.R, color.G, color.B), width))
                {
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, cardPath);
                }
            }
        }
    }
}
