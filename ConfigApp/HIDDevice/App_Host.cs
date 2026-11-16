using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConfigAppNS
{
    /// <summary>
    /// Main window: title bar actions (theme, language, swap, view), one header card per slot and
    /// two App_DDR panels (1P / 2P), side by side or as tabs. Owns USB enumeration
    /// (WM_DEVICECHANGE → PadManager.Rescan → panel.Attach / Detach) and the firmware upgrade flow.
    /// </summary>
    public sealed class App_Host : Form
    {
        private const int WM_DEVICECHANGE = 0x219;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        // Design metrics at 96 DPI (chrome scales with DPI only; the board panels scale with their width)
        private const int DesignMargin = 12;
        private const int TopBarH = 40;
        private const int HeaderH = 74;
        private const int Gap = 10;
        private const int PanelDesignW = 1040;
        private const int PanelDesignH = 780;

        private readonly PadManager mgr = new PadManager();
        private readonly App_DDR[] panels = new App_DDR[2];
        private readonly SlotHeader[] headers = new SlotHeader[2];
        private Button btnTheme, btnLang, btnSwap, btnView;
        private Label labelHint;
        private ContextMenuStrip menuLanguage;
        private ToolStripMenuItem menuLangAuto, menuLangZh, menuLangEn;
        private readonly Timer rescanTimer = new Timer();

        private bool dark;
        private Palette pal;
        private bool tabView;
        private int activeTab;
        private float dpiScale = 1f;
        private float fontStep = 1f;
        private bool layoutBusy;
        private bool otaBusy;
        private int otaTarget = -1;
        private bool rescanPending;
        private bool inRescan;

        public App_Host()
        {
            string theme = AppConfig.Read("THEME");
            dark = theme != null && theme.Trim().Equals("DARK", StringComparison.OrdinalIgnoreCase);
            string view = AppConfig.Read("DUALVIEW");
            tabView = view != null && view.Trim().Equals("TAB", StringComparison.OrdinalIgnoreCase);
            pal = Palette.For(dark);

            DebugLog.Log("==== {0} {1} start, config={2}, lang={3}, log={4}, view={5} ====",
                App_DDR.ApplicationName, App_DDR.HostVersion, AppConfig.Path, L.Setting, DebugLog.Enabled ? "on" : "off", tabView ? "TAB" : "SIDE");

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            ApplyAppIcon();
            dpiScale = ReadSystemDpiScale();

            BuildTopBar();
            for (int i = 0; i < 2; i++)
            {
                headers[i] = new SlotHeader(i);
                headers[i].Click += Header_Click;
                Controls.Add(headers[i]);

                App_DDR p = new App_DDR();
                p.SlotTag = mgr.Slots[i].Name;
                p.StateChanged += Panel_StateChanged;
                p.RescanRequested += (s, e) => Rescan();
                p.OtaRequested += Panel_OtaRequested;
                p.BusyChanged += Panel_BusyChanged;
                Controls.Add(p);
                panels[i] = p;
            }

            rescanTimer.Interval = 200;
            rescanTimer.Tick += (s, e) => { rescanTimer.Stop(); Rescan(); };

            L.LanguageChanged += (s, e) => { if (!IsDisposed) ApplyUiLanguage(); };

            // Initial size: two columns at 80 % of the single-window design, clamped to the working area
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int w = (int)((PanelDesignW * 2 * 0.8f + DesignMargin * 3) * dpiScale);
            int h = (int)((PanelDesignH * 0.8f + DesignMargin * 2 + TopBarH + HeaderH + Gap * 2) * dpiScale);
            if (w > work.Width - 40) w = work.Width - 40;
            if (h > work.Height - 80) h = work.Height - 80;
            ClientSize = new Size(w, h);
            MinimumSize = new Size((int)(900 * dpiScale), (int)(620 * dpiScale));
            ResumeLayout(false);

            ApplyUiLanguage();
            ApplyTheme();
            foreach (App_DDR p in panels) p.SetDarkMode(dark);
            LayoutAll();
            Resize += (s, e) => LayoutAll();
            FormClosing += App_Host_FormClosing;
            Shown += (s, e) => Rescan();
        }

        private void ApplyAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(Application.StartupPath, "AppLogo.ico");
                if (File.Exists(icoPath))
                {
                    Icon = new Icon(icoPath);
                    return;
                }
            }
            catch { }
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
            }
            catch { }
        }

        private float ReadSystemDpiScale()
        {
            float dpi = 96f;
            try { using (Graphics g = CreateGraphics()) dpi = g.DpiX; }
            catch { }
            float s = dpi / 96f;
            if (s < 1f) s = 1f;
            if (s > 2.5f) s = 2.5f;
            return s;
        }

        private int D(int designPx) { return (int)Math.Round(designPx * dpiScale); }

        // ---------------------------------------------------------------------------------------
        // Title bar
        // ---------------------------------------------------------------------------------------
        private void BuildTopBar()
        {
            btnTheme = MakeButton("btnThemeToggle", Btn_Theme_Click);
            btnLang = MakeButton("btnLangToggle", Btn_Lang_Click);
            btnSwap = MakeButton("btnSwap", Btn_Swap_Click);
            btnView = MakeButton("btnView", Btn_View_Click);

            labelHint = new Label();
            labelHint.AutoSize = false;
            labelHint.TextAlign = ContentAlignment.MiddleLeft;
            labelHint.AutoEllipsis = true;
            Controls.Add(labelHint);

            menuLanguage = new ContextMenuStrip();
            menuLanguage.ShowImageMargin = false;
            menuLangAuto = new ToolStripMenuItem();
            menuLangZh = new ToolStripMenuItem();
            menuLangEn = new ToolStripMenuItem();
            menuLangAuto.Click += (s, e) => SetLanguage(L.SettingAuto);
            menuLangZh.Click += (s, e) => SetLanguage(L.SettingChinese);
            menuLangEn.Click += (s, e) => SetLanguage(L.SettingEnglish);
            menuLanguage.Items.Add(menuLangAuto);
            menuLanguage.Items.Add(new ToolStripSeparator());
            menuLanguage.Items.Add(menuLangZh);
            menuLanguage.Items.Add(menuLangEn);
            menuLanguage.Opening += (s, e) => RefreshLanguageMenu();
        }

        private Button MakeButton(string name, EventHandler onClick)
        {
            Button b = new Button();
            b.Name = name;
            b.UseVisualStyleBackColor = false;
            b.Click += onClick;
            Controls.Add(b);
            return b;
        }

        private void Btn_Theme_Click(object sender, EventArgs e)
        {
            dark = !dark;
            AppConfig.Write("THEME", dark ? "DARK" : "LIGHT");
            ApplyTheme();
            foreach (App_DDR p in panels) p.SetDarkMode(dark);
        }

        private void Btn_Lang_Click(object sender, EventArgs e)
        {
            RefreshLanguageMenu();
            menuLanguage.Show(btnLang, new Point(0, btnLang.Height));
        }

        private void SetLanguage(string setting)
        {
            L.Apply(setting);                 // raises LanguageChanged → ApplyUiLanguage() here and in both panels
            AppConfig.Write("LANGUAGE", L.Setting);
        }

        private void RefreshLanguageMenu()
        {
            menuLangAuto.Text = L.Get("Lang.Auto");
            menuLangZh.Text = L.Get("Lang.zhCN");
            menuLangEn.Text = L.Get("Lang.en");
            menuLangAuto.Checked = L.Setting == L.SettingAuto;
            menuLangZh.Checked = L.Setting == L.SettingChinese;
            menuLangEn.Checked = L.Setting == L.SettingEnglish;
            menuLanguage.Renderer = new ToolStripProfessionalRenderer(new MenuColors(pal));
            menuLanguage.BackColor = pal.PanelBg;
            menuLanguage.ForeColor = pal.Text;
            foreach (ToolStripItem it in menuLanguage.Items)
            {
                it.BackColor = pal.PanelBg;
                it.ForeColor = pal.Text;
            }
            UiFont.Set(menuLanguage, UiFont.Body);
        }

        private void Btn_View_Click(object sender, EventArgs e)
        {
            tabView = !tabView;
            AppConfig.Write("DUALVIEW", tabView ? "TAB" : "SIDE");
            DebugLog.Log("VIEW: {0}", tabView ? "TAB" : "SIDE");
            ApplyUiLanguage();
            LayoutAll();
        }

        private void Header_Click(object sender, EventArgs e)
        {
            SlotHeader h = sender as SlotHeader;
            if (h == null) return;
            activeTab = h.Index;
            if (tabView) LayoutAll();
            UpdateHeaders();
        }

        /// <summary>
        /// "Swap 1P / 2P": the boards (and the remembered bindings) change columns. Both panels are
        /// re-attached, so each column reads its new board again (GET_MODE), as on a re-plug.
        /// </summary>
        private void Btn_Swap_Click(object sender, EventArgs e)
        {
            if (otaBusy || panels[0].IsBusy || panels[1].IsBusy) return;
            mgr.Swap();
            foreach (App_DDR p in panels) p.Detach();
            for (int i = 0; i < 2; i++) panels[i].Attach(mgr.Slots[i].Device);
            UpdateHeaders();
            UpdateTitle();
        }

        // ---------------------------------------------------------------------------------------
        // USB enumeration
        // ---------------------------------------------------------------------------------------
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DEVICECHANGE && (int)m.WParam == DBT_DEVNODES_CHANGED)
            {
                // Several notifications arrive per plug / unplug; coalesce them
                rescanTimer.Stop();
                rescanTimer.Start();
            }
            base.WndProc(ref m);
        }

        /// <summary>Enumerate USB and (re)bind both panels. Safe to call at any time on the UI thread.</summary>
        private void Rescan()
        {
            if (IsDisposed) return;
            if (otaBusy)
            {
                rescanPending = true;   // done once the upgrade dialog has closed
                return;
            }
            if (inRescan)
            {
                rescanPending = true;
                return;
            }
            inRescan = true;
            try
            {
                do
                {
                    rescanPending = false;
                    mgr.Rescan();
                    // Free the panels whose board left or moved before the receiving panel opens it
                    for (int i = 0; i < 2; i++)
                    {
                        HidDeviceInfo now = mgr.Slots[i].Device;
                        HidDeviceInfo had = panels[i].Device;
                        if (had != null && (now == null || now.BoardKey != had.BoardKey))
                            panels[i].Detach();
                    }
                    for (int i = 0; i < 2; i++)
                        panels[i].Attach(mgr.Slots[i].Device);
                } while (rescanPending);
            }
            finally
            {
                inRescan = false;
            }
            UpdateHeaders();
            UpdateTitle();
        }

        private void Panel_StateChanged(object sender, EventArgs e)
        {
            UpdateHeaders();
            UpdateTitle();
        }

        /// <summary>
        /// While one column waits for its board to restart (save → SET_MODE → re-enumerate) it pumps
        /// messages; the other column and the swap button are disabled so nothing else talks to
        /// USB or re-assigns slots in the middle of that wait.
        /// </summary>
        private void Panel_BusyChanged(object sender, EventArgs e)
        {
            bool anyBusy = panels[0].IsBusy || panels[1].IsBusy;
            for (int i = 0; i < 2; i++)
                panels[i].Enabled = otaBusy ? panels[i].Enabled : !(anyBusy && !panels[i].IsBusy);
            btnSwap.Enabled = !anyBusy && !otaBusy;
        }

        // ---------------------------------------------------------------------------------------
        // Firmware upgrade: one board at a time, the other column locked
        // ---------------------------------------------------------------------------------------
        private void Panel_OtaRequested(object sender, EventArgs e)
        {
            int slot = Array.IndexOf(panels, sender as App_DDR);
            RunOta(slot < 0 ? activeTab : slot);
        }

        private void RunOta(int preselect)
        {
            if (otaBusy) return;
            OtaTarget[] targets = new OtaTarget[2];
            for (int i = 0; i < 2; i++)
            {
                OtaTarget t = new OtaTarget();
                t.SlotName = mgr.Slots[i].Name;
                t.Device = panels[i].IsConnected ? panels[i].Device : null;
                t.FwValid = panels[i].FirmwareVersionValid;
                t.HVer = panels[i].DeviceHVer;
                t.FwMajor = panels[i].FirmwareMajor;
                t.FwMinor = panels[i].FirmwareMinor;
                targets[i] = t;
            }
            // Snapshot for the post-upgrade comparison (the panel forgets its version while the board is away)
            bool[] hadVersion = new bool[2];
            int[] hver = new int[2], major = new int[2], minor = new int[2];
            for (int i = 0; i < 2; i++)
            {
                hadVersion[i] = targets[i].FwValid;
                hver[i] = targets[i].HVer; major[i] = targets[i].FwMajor; minor[i] = targets[i].FwMinor;
            }

            otaBusy = true;
            otaTarget = -1;
            DebugLog.Log("OTA: dialog opened, preselected {0}", mgr.Slots[preselect].Name);
            App_OTA dlg = new App_OTA(targets, preselect);
            dlg.TargetChosen = slot =>
            {
                otaTarget = slot;
                DebugLog.Log("OTA: target {0}; locking {1}", mgr.Slots[slot].Name, mgr.Slots[1 - slot].Name);
                panels[slot].PrepareForOta();
                panels[1 - slot].SuspendIo(true);
                panels[1 - slot].Enabled = false;
                UpdateHeaders();
            };
            FirmwareImageInfo flashed = null;
            try
            {
                dlg.ShowDialog(this);
                flashed = dlg.FlashedImage;
            }
            finally
            {
                otaBusy = false;
                for (int i = 0; i < 2; i++)
                {
                    panels[i].SuspendIo(false);
                    panels[i].Enabled = true;
                }
            }
            int target = otaTarget >= 0 ? otaTarget : dlg.TargetIndex;
            if (flashed != null && target >= 0 && target < 2)
            {
                // Rebind first so the panel sees the APP as soon as it enumerates, then verify the version
                Rescan();
                panels[target].Verify_Firmware_AfterOta(flashed, hadVersion[target], hver[target], major[target], minor[target]);
            }
            otaTarget = -1;
            Rescan();
        }

        // ---------------------------------------------------------------------------------------
        // Layout / theme / language
        // ---------------------------------------------------------------------------------------
        private void LayoutAll()
        {
            if (layoutBusy || IsDisposed) return;
            layoutBusy = true;
            try
            {
                int m = D(DesignMargin);
                int gap = D(Gap);
                int W = ClientSize.Width, H = ClientSize.Height;

                // Title bar
                int bh = D(30), by = D(5);
                int bx = W - m;
                Button[] right = new Button[] { btnLang, btnTheme, btnView, btnSwap };
                int[] widths = new int[] { D(96), D(96), D(140), D(130) };
                for (int i = 0; i < right.Length; i++)
                {
                    bx -= widths[i];
                    right[i].Bounds = new Rectangle(bx, by, widths[i], bh);
                    bx -= D(8);
                }
                labelHint.Bounds = new Rectangle(m, by, Math.Max(10, bx - m), bh);

                int headerY = D(TopBarH) + gap;
                int contentY = headerY + D(HeaderH) + gap;
                int contentH = H - contentY - m;
                if (tabView)
                {
                    int tabW = (W - 2 * m - gap) / 2;
                    for (int i = 0; i < 2; i++)
                    {
                        headers[i].Bounds = new Rectangle(m + i * (tabW + gap), headerY, tabW, D(HeaderH));
                        headers[i].TabMode = true;
                        headers[i].Active = i == activeTab;
                        panels[i].Visible = i == activeTab;
                        panels[i].Bounds = FitPanel(new Rectangle(m, contentY, W - 2 * m, contentH));
                    }
                }
                else
                {
                    int colW = (W - 2 * m - gap) / 2;
                    for (int i = 0; i < 2; i++)
                    {
                        int x = m + i * (colW + gap);
                        headers[i].Bounds = new Rectangle(x, headerY, colW, D(HeaderH));
                        headers[i].TabMode = false;
                        headers[i].Active = false;
                        panels[i].Visible = true;
                        panels[i].Bounds = FitPanel(new Rectangle(x, contentY, colW, contentH));
                    }
                }
                UpdateFontStep();
            }
            finally
            {
                layoutBusy = false;
            }
        }

        /// <summary>A board panel keeps the 1040×780 aspect; when the column is wider than that it is centred.</summary>
        private static Rectangle FitPanel(Rectangle col)
        {
            int needW = (int)Math.Round(col.Height * (float)PanelDesignW / PanelDesignH);
            if (needW < col.Width)
            {
                col.X += (col.Width - needW) / 2;
                col.Width = needW;
            }
            return col;
        }

        /// <summary>Fonts shrink with the column: step of 0.05 between 0.7 and 1.0 of the design size.</summary>
        private void UpdateFontStep()
        {
            float sx = (float)panels[0].Width / PanelDesignW;
            float sy = (float)panels[0].Height / PanelDesignH;
            float s = Math.Min(sx, sy) / dpiScale;
            if (s > 1f) s = 1f;
            if (s < 0.7f) s = 0.7f;
            float step = (float)Math.Round(s / 0.05f) * 0.05f;
            if (Math.Abs(step - fontStep) < 0.001f) return;
            fontStep = step;
            UiFont.Scale = step;
            foreach (App_DDR p in panels) p.RefreshFonts();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            pal = Palette.For(dark);
            BackColor = pal.FormBg;
            ForeColor = pal.Text;
            pal.StyleButton(btnLang, pal.Neutral, pal.NeutralText, dark);
            pal.StyleButton(btnTheme, pal.Accent, pal.AccentText, dark);
            pal.StyleButton(btnSwap, pal.Neutral, pal.NeutralText, dark);
            pal.StyleButton(btnView, pal.Neutral, pal.NeutralText, dark);
            labelHint.BackColor = pal.FormBg;
            labelHint.ForeColor = pal.MutedText;
            UiFont.Set(labelHint, UiFont.Status);
            foreach (SlotHeader h in headers)
            {
                h.Pal = pal;
                h.Invalidate();
            }
            btnTheme.Text = L.Get(dark ? "Btn.ThemeLight" : "Btn.ThemeDark");
            Invalidate(true);
        }

        private void ApplyUiLanguage()
        {
            btnLang.Text = L.Get("Btn.Language");
            btnTheme.Text = L.Get(dark ? "Btn.ThemeLight" : "Btn.ThemeDark");
            btnSwap.Text = L.Get("Btn.Swap");
            btnView.Text = L.Get(tabView ? "Btn.ViewSide" : "Btn.ViewTab");
            RefreshLanguageMenu();
            UpdateHeaders();
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string[] st = new string[2];
            for (int i = 0; i < 2; i++)
            {
                if (otaBusy && otaTarget == i) st[i] = L.Get("Slot.Upgrading");
                else st[i] = L.Get(panels[i].IsConnected ? "Slot.Connected" : "Slot.NoDevice");
            }
            Text = App_DDR.ApplicationName + " " + App_DDR.HostVersion + " " + L.F("Title.Slots", st[0], st[1]);

            string hint = "";
            if (mgr.BootloaderCount > 0) hint = L.F("Host.Bootloader", mgr.BootloaderCount);
            else if (mgr.UnplacedCount > 0) hint = L.F("Host.Unplaced", mgr.UnplacedCount);
            labelHint.Text = hint;
        }

        private void UpdateHeaders()
        {
            for (int i = 0; i < 2; i++)
            {
                SlotHeader h = headers[i];
                App_DDR p = panels[i];
                PadSlot slot = mgr.Slots[i];
                h.Name1 = slot.Name;
                if (otaBusy && otaTarget == i)
                {
                    h.State = SlotHeader.SlotState.Upgrading;
                    h.Line1 = L.Get("Slot.Upgrading");
                    h.Line2 = slot.Device != null ? SerialText(slot.Device) : "";
                }
                else if (otaBusy && otaTarget >= 0)
                {
                    h.State = SlotHeader.SlotState.Locked;
                    h.Line1 = L.Get("Slot.Locked");
                    h.Line2 = slot.Device != null ? SerialText(slot.Device) : "";
                }
                else if (p.IsConnected)
                {
                    h.State = SlotHeader.SlotState.Connected;
                    string mode = p.ModeSummary();
                    h.Line1 = L.Get("Slot.Connected") + (mode.Length > 0 ? " · " + mode : "");
                    h.Line2 = p.VersionSummary() + (slot.Device != null ? "   " + SerialText(slot.Device) : "");
                }
                else if (slot.Device != null)
                {
                    h.State = SlotHeader.SlotState.Error;
                    h.Line1 = L.Get("Slot.OpenFailed");
                    h.Line2 = SerialText(slot.Device);
                }
                else
                {
                    h.State = SlotHeader.SlotState.Empty;
                    h.Line1 = L.Get("Slot.NoDevice");
                    h.Line2 = L.Get("Slot.PlugHint");
                }
                h.Invalidate();
            }
        }

        private static string SerialText(HidDeviceInfo d)
        {
            return L.F("Slot.Serial", d.Serial.Length > 0 ? d.Serial : d.PortKey);
        }

        private void App_Host_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (App_DDR p in panels)
            {
                try { p.Shutdown(); } catch { }
            }
            DebugLog.Log("==== exit ====");
        }

        // ---------------------------------------------------------------------------------------
        /// <summary>Owner-drawn card above each column: slot badge, status dot, two text lines.</summary>
        private sealed class SlotHeader : Control
        {
            public enum SlotState { Empty, Connected, Error, Locked, Upgrading }
            public readonly int Index;
            public string Name1 = "1P";
            public string Line1 = "", Line2 = "";
            public SlotState State = SlotState.Empty;
            public bool TabMode, Active;
            public Palette Pal = Palette.Light();

            public SlotHeader(int index)
            {
                Index = index;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(Parent != null ? Parent.BackColor : Pal.FormBg);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                int radius = Math.Max(4, Height / 9);
                using (System.Drawing.Drawing2D.GraphicsPath path = PadKeyPainter.RoundedRect(r, radius))
                {
                    using (SolidBrush fill = new SolidBrush(TabMode && !Active ? Pal.FormBg : Pal.PanelBg))
                        g.FillPath(fill, path);
                    using (Pen pen = new Pen(TabMode && Active ? Pal.Accent : Pal.Border, TabMode && Active ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
                if (TabMode && Active)
                {
                    Rectangle ul = new Rectangle(r.X + radius, r.Bottom - 3, r.Width - 2 * radius, 3);
                    using (SolidBrush b = new SolidBrush(Pal.Accent)) g.FillRectangle(b, ul);
                }

                // Badge
                int pad = Math.Max(8, Height / 8);
                int badgeH = Height - 2 * pad;
                int badgeW = (int)(badgeH * 1.15f);
                Rectangle badge = new Rectangle(pad, pad, badgeW, badgeH);
                Color badgeBack = Index == 0 ? Pal.Accent : Pal.Selected;
                using (System.Drawing.Drawing2D.GraphicsPath bp = PadKeyPainter.RoundedRect(badge, Math.Max(4, badgeH / 5)))
                using (SolidBrush b = new SolidBrush(badgeBack))
                    g.FillPath(b, bp);
                using (Font f = UiFont.Get(UiFont.Value + 4f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Name1, f, tb, badge, sf);
                }

                // Status dot + lines
                int x = badge.Right + pad;
                int dot = Math.Max(8, Height / 7);
                Color dotColor;
                switch (State)
                {
                    case SlotState.Connected: dotColor = Pal.Ok; break;
                    case SlotState.Upgrading: dotColor = Pal.Warn; break;
                    case SlotState.Locked: dotColor = Pal.MutedText; break;
                    case SlotState.Error: dotColor = Pal.Danger; break;
                    default: dotColor = Pal.BorderStrong; break;
                }
                int line1Y = pad;
                using (Font f1 = UiFont.Get(UiFont.GroupTitle, FontStyle.Bold))
                using (Font f2 = UiFont.Get(UiFont.Status))
                {
                    int h1 = (int)Math.Ceiling(f1.GetHeight(g));
                    using (SolidBrush db = new SolidBrush(dotColor))
                        g.FillEllipse(db, x, line1Y + (h1 - dot) / 2, dot, dot);
                    Rectangle l1 = new Rectangle(x + dot + 6, line1Y, Width - (x + dot + 6) - pad, h1);
                    using (SolidBrush tb = new SolidBrush(State == SlotState.Connected ? Pal.Text : Pal.MutedText))
                        g.DrawString(Line1, f1, tb, l1, Ellipsis());
                    Rectangle l2 = new Rectangle(x, line1Y + h1 + 2, Width - x - pad, Height - (line1Y + h1 + 2) - pad / 2);
                    using (SolidBrush tb = new SolidBrush(Pal.MutedText))
                        g.DrawString(Line2, f2, tb, l2, Ellipsis());
                }
            }

            private static StringFormat Ellipsis()
            {
                StringFormat sf = new StringFormat();
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.LineAlignment = StringAlignment.Near;
                return sf;
            }
        }
    }
}
