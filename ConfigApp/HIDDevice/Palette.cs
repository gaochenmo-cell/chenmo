using System.Drawing;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Light: Laverita palette (Tailwind gray scale + blue / amber / red accents).
    /// Dark: neon purple (deep violet-black background, violet→pink gradient highlights with glow).
    /// Gradient / glow fields are Color.Empty on the light palette, which disables those effects.
    /// Shared by the main window and the two board panels so both columns always match.
    /// </summary>
    internal sealed class Palette
    {
        public Color FormBg, PanelBg, CardBg, Border, BorderStrong, Text, MutedText, Title;
        public Color Accent, AccentText, Danger, Neutral, NeutralText, Selected, BarTrack, BarFill;
        public Color PadOutline;          // arrow / ring stroke on an unselected pad key
        public Color SelectedGradientEnd; // second stop of the selected pad key fill (Empty = solid)
        public Color AccentGradientEnd;   // second stop of the tab underline (Empty = solid)
        public Color Glow;                // outer glow around selected / pressed pad keys (Empty = none)
        public Color ArrowTip, ArrowMid, ArrowBack, ArrowGlow; // chevron / ring cyan gradient + halo
        public Color Ok, Warn;            // connection dots on the 1P / 2P header cards

        public static Palette For(bool dark)
        {
            return dark ? Dark() : Light();
        }

        public static Palette Light()
        {
            Palette p = new Palette();
            p.FormBg = Color.FromArgb(243, 244, 246);
            p.PanelBg = Color.White;
            p.CardBg = Color.FromArgb(249, 250, 251);
            p.Border = Color.FromArgb(229, 231, 235);
            p.BorderStrong = Color.FromArgb(209, 213, 219);
            p.Text = Color.FromArgb(31, 41, 55);
            p.MutedText = Color.FromArgb(107, 114, 128);
            p.Title = Color.FromArgb(47, 111, 237);
            p.Accent = Color.FromArgb(47, 111, 237);
            p.AccentText = Color.White;
            p.Danger = Color.FromArgb(220, 38, 38);
            p.Neutral = Color.FromArgb(229, 231, 235);
            p.NeutralText = Color.FromArgb(31, 41, 55);
            p.Selected = Color.FromArgb(245, 158, 11);
            p.BarTrack = Color.FromArgb(229, 231, 235);
            p.BarFill = Color.FromArgb(200, 220, 245);
            p.PadOutline = Color.FromArgb(31, 41, 55);
            p.SelectedGradientEnd = Color.Empty;
            p.AccentGradientEnd = Color.Empty;
            p.Glow = Color.Empty;
            // Same chevron shape as dark mode, one step deeper so it reads on the light card
            p.ArrowTip = Color.FromArgb(0x06, 0xB6, 0xD4);   // #06B6D4
            p.ArrowMid = Color.FromArgb(0x08, 0x91, 0xB2);   // #0891B2
            p.ArrowBack = Color.FromArgb(0x15, 0x5E, 0x75);  // #155E75
            p.ArrowGlow = Color.FromArgb(0x0E, 0x74, 0x90);  // #0E7490
            p.Ok = Color.FromArgb(0x16, 0xA3, 0x4A);         // #16A34A
            p.Warn = Color.FromArgb(0xF5, 0x9E, 0x0B);       // #F59E0B
            return p;
        }

        public static Palette Dark()
        {
            Palette p = new Palette();
            p.FormBg = Color.FromArgb(13, 10, 31);          // #0D0A1F deep violet-black
            p.PanelBg = Color.FromArgb(23, 18, 51);         // #171233 card
            p.CardBg = Color.FromArgb(36, 29, 74);          // #241D4A inner cell / input
            p.Border = Color.FromArgb(52, 42, 102);         // #342A66
            p.BorderStrong = Color.FromArgb(91, 74, 160);   // #5B4AA0
            p.Text = Color.FromArgb(237, 233, 254);         // #EDE9FE
            p.MutedText = Color.FromArgb(157, 146, 201);    // #9D92C9 violet-grey
            p.Title = Color.FromArgb(192, 132, 252);        // #C084FC
            p.Accent = Color.FromArgb(124, 58, 237);        // #7C3AED
            p.AccentText = Color.White;
            p.Danger = Color.FromArgb(236, 72, 153);        // #EC4899 neon pink
            p.Neutral = Color.FromArgb(42, 34, 80);         // #2A2250
            p.NeutralText = Color.FromArgb(237, 233, 254);
            p.Selected = Color.FromArgb(124, 58, 237);      // #7C3AED → gradient end below
            p.BarTrack = Color.FromArgb(36, 29, 74);
            p.BarFill = Color.FromArgb(91, 74, 160);
            p.PadOutline = Color.FromArgb(196, 181, 253);   // #C4B5FD lavender
            p.SelectedGradientEnd = Color.FromArgb(236, 72, 153); // #EC4899
            p.AccentGradientEnd = Color.FromArgb(96, 165, 250);   // #60A5FA
            p.Glow = Color.FromArgb(168, 85, 247);          // #A855F7
            p.ArrowTip = Color.FromArgb(0x22, 0xD3, 0xEE);   // #22D3EE
            p.ArrowMid = Color.FromArgb(0x06, 0xB6, 0xD4);   // #06B6D4
            p.ArrowBack = Color.FromArgb(0x0E, 0x74, 0x90);  // #0E7490
            p.ArrowGlow = Color.FromArgb(0x22, 0xD3, 0xEE);  // #22D3EE
            p.Ok = Color.FromArgb(0x4A, 0xDE, 0x80);         // #4ADE80
            p.Warn = Color.FromArgb(0xFB, 0xBF, 0x24);       // #FBBF24
            return p;
        }

        /// <summary>amount &gt; 0 lightens, &lt; 0 darkens.</summary>
        public static Color Lighten(Color c, float amount)
        {
            float r = c.R, g = c.G, b = c.B;
            if (amount >= 0)
            {
                r += (255 - r) * amount; g += (255 - g) * amount; b += (255 - b) * amount;
            }
            else
            {
                r *= 1 + amount; g *= 1 + amount; b *= 1 + amount;
            }
            return Color.FromArgb(c.A, (int)r, (int)g, (int)b);
        }

        /// <summary>Flat accent-coloured button, same look in both columns and in the title bar.</summary>
        public void StyleButton(Button b, Color back, Color fore, bool dark)
        {
            if (b == null)
                return;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Lighten(back, dark ? 0.12f : -0.08f);
            b.FlatAppearance.MouseDownBackColor = Lighten(back, dark ? 0.2f : -0.16f);
            b.UseVisualStyleBackColor = false;
            b.BackColor = back;
            b.ForeColor = fore;
            UiFont.Set(b, UiFont.Body, FontStyle.Bold);
        }
    }

    /// <summary>Drop-down menu colours that follow the active palette (readable in dark mode too).</summary>
    internal sealed class MenuColors : ProfessionalColorTable
    {
        private readonly Palette p;
        public MenuColors(Palette pal) { p = pal; }
        public override Color MenuBorder { get { return p.Border; } }
        public override Color MenuItemBorder { get { return p.Accent; } }
        public override Color MenuItemSelected { get { return p.CardBg; } }
        public override Color MenuItemSelectedGradientBegin { get { return p.CardBg; } }
        public override Color MenuItemSelectedGradientEnd { get { return p.CardBg; } }
        public override Color MenuItemPressedGradientBegin { get { return p.CardBg; } }
        public override Color MenuItemPressedGradientEnd { get { return p.CardBg; } }
        public override Color ToolStripDropDownBackground { get { return p.PanelBg; } }
        public override Color ImageMarginGradientBegin { get { return p.PanelBg; } }
        public override Color ImageMarginGradientMiddle { get { return p.PanelBg; } }
        public override Color ImageMarginGradientEnd { get { return p.PanelBg; } }
        public override Color SeparatorDark { get { return p.Border; } }
        public override Color SeparatorLight { get { return p.PanelBg; } }
        public override Color CheckBackground { get { return p.Accent; } }
        public override Color CheckSelectedBackground { get { return p.Accent; } }
        public override Color CheckPressedBackground { get { return p.Accent; } }
    }
}
