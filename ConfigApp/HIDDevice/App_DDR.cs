using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
//using Microsoft.Office.Interop.Excel;
using HIDLibraryNS;


namespace ConfigAppNS
{
    public partial class App_DDR : Form
    {

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        ///-------------------------------------------------------------------------------------------
        HidLibrary USBCtrl = new HidLibrary();

        private delegate void UpdateUSBHIDStateDelegate(HIDLibraryLibChangedEventArgs args);

        ///-------------------------------------------------------------------------------------------
        private bool bDeviceStatusFG = false;
        //private bool bTouchTestMode = false;
        private bool bLanguageEN = false;
        private bool bDarkMode = false;
        private bool bDebugModesFG = false;
        private readonly bool[] padKeyActive = new bool[5];
        private byte mDeviceIndex = 0;
        private byte DefaultOffset = 0x01;			//V1.0.0.18:20241012(1)Change ADX Save Issue
        ///-------------------------------------------------------------------------------------------
        private string AppName = "DDR Config";

        ///-------------------------------------------------------------------------------------------
        //System.IO.FileInfo[] files;
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== Windows Message ====
        /// =============================================================================================
        /// 
        /// =============================================================================================
        //委托
        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        static int hHook = 0;
        public const int WH_KEYBOARD_LL = 13;
        //LowLevel键盘截获，如果是WH_KEYBOARD＝2，并不能对系统键盘截取，Acrobat Reader会在你截取之前获得键盘。
        HookProc KeyBoardHookProcedure;
        public const int KEYS_TAB = 9;
        public const int KEYS_LEFT = 0x25;
        public const int KEYS_RIGHT = 0x27;
        public const int KEYS_ENTER = 0x0d;
        KeyBoardHookStruct kbhBK;
        private bool bYesFouceStatusFG = false, bYesFouceStatusBKFG = false;
        private const int WH_KEYBOARD = 13;
        /// =============================================================================================
        ///
        // Constant value was found in the "windows.h" header file.
        public const int WM_DEVICECHANGE = 0x219;
        public const int WM_ACTIVATEAPP = 0x001C;
        public const int DBT_DEVICEARRIVAL = 0x8000;            //U盘插入
        public const int DBT_CONFIGCHANGECANCELED = 0x0019;     //A request to change the current configuration (dock or undock) has been canceled
        public const int DBT_CONFIGCHANGED = 0x0018;            //The current configuration has changed, due to a dock or undock.
        public const int DBT_CUSTOMEVENT = 0x8006;              //A custom event has occurred
        public const int DBT_DEVICEQUERYREMOVE = 0x8001;        //Permission is requested to remove a device or piece of media. Any application can deny this request and cancel the removal.
        public const int DBT_DEVICEQUERYREMOVEFAILED = 0x8002;  //A request to remove a device or piece of media has been canceled.
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;     //U盘卸载
        public const int DBT_DEVICEREMOVEPENDING = 0x8003;      //A device or piece of media is about to be removed. Cannot be denied
        public const int DBT_DEVICETYPESPECIFIC = 0x8005;       //A device-specific event has occurred.
        public const int DBT_DEVNODES_CHANGED = 0x0007;         //A device has been added to or removed from the system
        public const int DBT_QUERYCHANGECONFIG = 0x0017;        //Permission is requested to change the current configuration (dock or undock).
        public const int DBT_USERDEFINED = 0xFFFF;

        /// =============================================================================================
        ///
        //键盘Hook结构函数
        [StructLayout(LayoutKind.Sequential)]
        public class KeyBoardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        /// =============================================================================================
        ///
        #region DllImport
        //设置钩子
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        //抽掉钩子
        public static extern bool UnhookWindowsHookEx(int idHook);
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        //调用下一个钩子

        //[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        //public static extern IntPtr CallNextHookEx(IntPtr hhook, int code, IntPtr wparam, IntPtr lparam);
        public static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        //[DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
        // public static extern void CopyMemory(ref KBDLLHOOKSTRUCT Source, IntPtr Destination, int Length);
        #endregion

        /// =============================================================================================
        /// ==== Windows HookProc Start ====
        /// =============================================================================================
        ///
        //#region 自定义事件
        public void Hook_Start()
        {
            // 安装键盘钩子
            KeyBoardHookProcedure = new HookProc(KeyBoardHookProc);
            //_hookProcHandle = GCHandle.Alloc(KeyBoardHookProcedure);
            hHook = SetWindowsHookEx(WH_KEYBOARD, KeyBoardHookProcedure, GetModuleHandle("HookDll.dll"), 0);
            if (hHook == 0)
            {
                //throw new System.Exception("错误,钩子失败!");
            }
        }

        /// =============================================================================================
        /// ==== Windows HookProc Clear ====        
        /// =============================================================================================
        ///
        public void Hook_Clear()//取消钩子事件
        {
            bool retKeyboard = true;
            if (hHook != 0)
            {
                retKeyboard = UnhookWindowsHookEx(hHook);
                hHook = 0;
            }
            //如果去掉钩子失败.
            if (!retKeyboard) throw new Exception("UnhookWindowsHookEx failed.");
        }

        /// =============================================================================================
        /// ==== Windows KeyBoard HookProc ====
        /// =============================================================================================
        ///
        //这里可以添加自己想要的信息处理
        public int KeyBoardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KeyBoardHookStruct kbh = (KeyBoardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyBoardHookStruct));
                if (kbhBK != kbh)
                {
                    kbhBK = kbh;
                    if ((kbh.vkCode == (int)KEYS_ENTER))
                    {
                        if (bYesFouceStatusFG != bYesFouceStatusBKFG)
                        {
                            bYesFouceStatusBKFG = bYesFouceStatusFG;
                        }
                        else
                        {
                            if (bYesFouceStatusFG == false) bYesFouceStatusFG = true;
                            else bYesFouceStatusFG = false;
                        }
                    }
                }
            }
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }
        //#endregion


        /// =============================================================================================
        /// ==== Window Proc ====
        /// =============================================================================================
        ///
        //[System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            //int bStatus = (int)DeviceType.None;
            //bDeviceStatusFG = false;
            if (m.Msg == WM_DEVICECHANGE)//WM_DEVICECHANGE   DBT_DEVICEARRIVAL
            {
                if (((int)m.WParam == DBT_DEVICEARRIVAL))
                {
                    //Logtxtbx.Text += "device has been added to the system \r\n";
                    //Logtxtbx.SelectionStart = Logtxtbx.Text.Length;
                    //Logtxtbx.ScrollToCaret();
                }
                else if (((int)m.WParam == DBT_DEVICEREMOVECOMPLETE))
                {
                    //Logtxtbx.Text += "device has been removed from the system \r\n";
                    //Logtxtbx.SelectionStart = Logtxtbx.Text.Length;
                    //Logtxtbx.ScrollToCaret();
                }
                else if (//((int)m.WParam == DBT_DEVICEARRIVAL) ||
                    //((int)m.WParam == DBT_DEVICEREMOVEPENDING) ||
                    //((int)m.WParam == DBT_DEVICEREMOVECOMPLETE) ||
                    //((int)m.WParam == DBT_CONFIGCHANGED) ||
                    ((int)m.WParam == DBT_DEVNODES_CHANGED))
                {
                    Find_USB_Deivce();
                }
            }
            base.WndProc(ref m);
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        public class ProgressBarWithValue : ProgressBar
        {
            private ProgressBarDirection direction = ProgressBarDirection.Horizontal;
            private bool showPercent = true;
             private string customText;
     
            public ProgressBarWithValue()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                base.Size = new Size(200, 15);
            }
     
             protected override void OnPaint(PaintEventArgs e)
             {
                 Rectangle rect = ClientRectangle;
                 Graphics g = e.Graphics;
                 if (direction == ProgressBarDirection.Horizontal)
                     ProgressBarRenderer.DrawHorizontalBar(g, rect);
                 else
                     ProgressBarRenderer.DrawVerticalBar(g, rect);
                 rect.Inflate(-3, -3);
                 if (Value > 0)
                 {
                     if (direction == ProgressBarDirection.Horizontal)
                     {
                         Rectangle clip = new Rectangle(rect.X, rect.Y, (int)Math.Round(((float)Value / Maximum) * rect.Width), rect.Height);
                         ProgressBarRenderer.DrawHorizontalChunks(g, clip);
                     }
                     else
                     {
                         int height = (int)Math.Round(((float)Value / Maximum) * rect.Height);
                         Rectangle clip = new Rectangle(rect.X, rect.Y + (rect.Height - height), rect.Width, height);
                         ProgressBarRenderer.DrawVerticalChunks(g, clip);
                     }
                 }
     
                 //string text = showPercent ? (Value.ToString() + '%') : CustomText;
                 //float per = (float)((float)Value * 100) / (float)Maximum;
                 //string text = per.ToString("F1") + '%' +"\r\n("+ Value.ToString()+")";
                 string text =  Value.ToString() ;
     
                 if (!string.IsNullOrEmpty(text))
                     using (Font f = new Font(FontFamily.GenericSerif, 10))
                     {
                         SizeF len = g.MeasureString(text, f);
                         Point location = new Point(Convert.ToInt32((Width / 2) - len.Width / 2), Convert.ToInt32((Height / 2) - len.Height / 2));
                         g.DrawString(text, f, Brushes.Black, location);
                     }
             }
     
     
             /// <summary>
             /// 进度条方向，水平或者垂直
             /// </summary>
             public ProgressBarDirection Direction
             {
                 get { return direction; }
                 set
                 {
                     if (direction != value)
                     {
                         direction = value;
                         Invalidate();
                     }
                 }
             }
     
             /// <summary>
             /// 是否显示进度，true表示显示进度，否则显示自定义的字符串
             /// </summary>
             public bool ShowPercent
             {
                 get { return showPercent; }
                 set
                 {
                     showPercent = value;
                 }
             }
     
             /// <summary>
             /// 自定义需要显示的字符串
             /// </summary>
             public string CustomText
             {
                 get { return customText; }
                 set
                 {
                     if (customText != value)
                     {
                         customText = value;
                         if (!showPercent)
                             Invalidate();
                     }
                 }
             }
         }
     
         public enum ProgressBarDirection
         {
             /// <summary>
             /// 垂直方向
             /// </summary>
             Vertical,
             /// <summary>
             /// 水平方向
             /// </summary>
             Horizontal,
         }

         public class VerticalProgressBar : ProgressBar
         {
             protected override CreateParams CreateParams
             {
                 get
                 {
                     CreateParams cp = base.CreateParams;
                     cp.Style |= 0x04;
                     return cp;
                 }
             }
         }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        /// =============================================================================================
        /// ====  DDR Init  ====
        /// =============================================================================================
        ///
        public App_DDR()
        {
            ///------------------------------------------------------------------------------------------
            InitializeComponent();
            ApplyAppIcon();
            ///------------------------------------------------------------------------------------------
            ReadAppConfig();
            ///------------------------------------------------------------------------------------------
            USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            Find_USB_Deivce();
            ///------------------------------------------------------------------------------------------
            timer1.Enabled = true;
            timer1.Interval = 10; // ~100 Hz sensor poll in debug / live pad highlight
            ///------------------------------------------------------------------------------------------
            Load_Default_Sens();
            groupBox_Button.Visible = true;
            groupBox_Sensi.Enabled = false;
            groupBox_Color.Enabled = false;
            groupBox_DTick.Enabled = false;

            ///------------------------------------------------------------------------------------------
            ApplyFiveKeyLayoutOnly();  // Yuanpad 5Key only — real PIU corner+center layout + DPI
            ///------------------------------------------------------------------------------------------
            tabControl1.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            progressBar1.Direction = ProgressBarDirection.Vertical;
            progressBar2.Direction = ProgressBarDirection.Vertical;
            progressBar3.Direction = ProgressBarDirection.Vertical;
            progressBar4.Direction = ProgressBarDirection.Vertical;
            ///------------------------------------------------------------------------------------------
        }

        private void ApplyAppIcon()
        {
            try
            {
                string icoPath = Path.Combine(Application.StartupPath, "AppLogo.ico");
                if (File.Exists(icoPath))
                {
                    this.Icon = new Icon(icoPath);
                    return;
                }
            }
            catch { }
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null)
                    this.Icon = ico;
            }
            catch { }
        }

        /// =============================================================================================
        /// ==== Yuanpad 5Key layout only (matches physical pad) ====
        ///   [1 UL] [    ] [2 UR]
        ///   [    ] [3 C ] [    ]
        ///   [4 DL] [    ] [5 DR]
        /// =============================================================================================
        private const int KEY_COUNT_5KEY = 5;
        private const int SENSORS_PER_KEY = 4;
        private const int CONFIG_VALUE_COUNT = KEY_COUNT_5KEY * SENSORS_PER_KEY; // 20 — FW DATA_LEN/2
        private Panel[] fiveKeyFillers = null;
        // Design canvas at 96 DPI — all layout math uses S(designPx)
        private const int DesignW = 1040;
        private const int DesignH = 780;
        private float layoutScale = 1f;
        private bool layoutBusy = false;
        private bool layoutReady = false;
        private bool padKeyVectorWired = false;
        private Button btnFirmwareUpgrade;
        private Button btnLangToggle;
        private Button btnThemeToggle;
        private TabPage tabPage_Map;
        private GroupBox groupBox_Map;
        private Label label_MapHint;
        private Label label_MapKey;
        private ComboBox[] mapKeyCombos;
        private Button btn_MapRead;
        private Button btn_MapSave;
        private readonly int[] KeyMappingBuffer = new int[KEY_COUNT_5KEY];
        // Default HID usages = stock Report_Normal 1P Index0..4: W X A D S
        private readonly int[] DefaultKeyMapping = new int[] { 0x1A, 0x1B, 0x04, 0x07, 0x16 };

        private int S(int px)
        {
            return (int)Math.Round(px * layoutScale);
        }

        private float ReadSystemDpiScale()
        {
            float dpi = 96f;
            try
            {
                using (Graphics g = this.CreateGraphics())
                    dpi = g.DpiX;
            }
            catch { }
            float s = dpi / 96f;
            if (s < 1f) s = 1f;
            if (s > 2.5f) s = 2.5f;
            return s;
        }

        private void ApplyFiveKeyLayoutOnly()
        {
            this.Text = "PADUEX5";
            this.AutoScaleMode = AutoScaleMode.None;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;

            // One-time chrome / hide unused controls
            pictureBox1.Visible = false; pictureBox1.Enabled = false;
            if (groupBox_Sensi.Controls.Contains(pictureBox1))
                groupBox_Sensi.Controls.Remove(pictureBox1);
            pictureBox_sens.Visible = false; pictureBox_sens.Enabled = false;
            if (tabPage_Color.Controls.Contains(pictureBox_sens))
                tabPage_Color.Controls.Remove(pictureBox_sens);

            Btn_Col_G.Visible = Btn_Col_Y.Visible = Btn_Col_P.Visible = Btn_Col_C.Visible = false;
            Btn_Col_G.Enabled = Btn_Col_Y.Enabled = Btn_Col_P.Enabled = Btn_Col_C.Enabled = false;
            Btn_K6.Visible = Btn_K7.Visible = Btn_K8.Visible = Btn_K9.Visible = false;
            Btn_K6.Enabled = Btn_K7.Enabled = Btn_K8.Enabled = Btn_K9.Enabled = false;
            radioButton_IO4.Visible = false;

            EnsureFirmwareUpgradeButton();
            EnsureLanguageToggleButton();
            EnsureThemeToggleButton();
            EnsureKeyMappingUi();
            EnsurePadKeyVectorPaint();
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
                KeyMappingBuffer[i] = DefaultKeyMapping[i];
            if (btnFirmwareUpgrade.Parent != groupBox_Mode)
            {
                this.Controls.Remove(btnFirmwareUpgrade);
                groupBox_Mode.Controls.Add(btnFirmwareUpgrade);
            }

            // Initial window = design × system DPI; later Resize keeps aspect scale
            float dpiScale = ReadSystemDpiScale();
            layoutBusy = true;
            this.ClientSize = new Size((int)(DesignW * dpiScale), (int)(DesignH * dpiScale));
            this.MinimumSize = new Size((int)(DesignW * dpiScale * 0.85f) + 16,
                                        (int)(DesignH * dpiScale * 0.85f) + 40);
            layoutScale = dpiScale;
            RebuildScaledLayout();
            layoutBusy = false;
            layoutReady = true;

            this.Resize -= App_DDR_Resize;
            this.Resize += App_DDR_Resize;

            ApplyUiLanguage();
            ApplyTheme();
            SensorIndex = 0;
        }

        private void App_DDR_Resize(object sender, EventArgs e)
        {
            if (!layoutReady || layoutBusy)
                return;
            layoutBusy = true;
            try
            {
                float sx = (float)this.ClientSize.Width / DesignW;
                float sy = (float)this.ClientSize.Height / DesignH;
                float s = Math.Min(sx, sy);
                if (s < 0.7f) s = 0.7f;
                if (s > 3.0f) s = 3.0f;
                layoutScale = s;
                RebuildScaledLayout();
            }
            finally
            {
                layoutBusy = false;
            }
        }

        /// <summary>
        /// Place every control from the 1040×780 design canvas using layoutScale.
        /// Maximizing / resizing keeps proportions; Sensitivity A–D stay fully visible.
        /// </summary>
        private void RebuildScaledLayout()
        {
            // Color: Red / White / Blue (white centered)
            Btn_Col_R.Visible = Btn_Col_B.Visible = Btn_Col_W.Visible = true;
            Btn_Col_R.Enabled = Btn_Col_B.Enabled = Btn_Col_W.Enabled = true;
            Btn_Col_R.Location = new Point(S(40), S(28));
            Btn_Col_W.Location = new Point(S(120), S(28));
            Btn_Col_B.Location = new Point(S(200), S(28));
            Btn_Col_R.Size = Btn_Col_B.Size = Btn_Col_W.Size = new Size(S(60), S(60));
            groupBox_Color.Location = new Point(S(16), S(30));
            groupBox_Color.Size = new Size(S(300), S(100));
            Btn_LED.Location = new Point(S(340), S(40));
            Btn_LED.Size = new Size(S(100), S(100));

            // Sensitivity A–D compact columns (must fit inside tab ~500 design px)
            LayoutSensitivityColumns();

            groupBox_DTick.Location = new Point(S(24), S(36));
            groupBox_DTick.Size = new Size(S(460), S(150));
            trackBar_DTick.Location = new Point(S(16), S(28));
            trackBar_DTick.Size = new Size(S(420), S(45));
            label_DTick.Location = new Point(S(80), S(90));
            label_DTick.Size = new Size(S(180), S(36));
            textBox_DTick.Location = new Point(S(270), S(92));
            textBox_DTick.Size = new Size(S(72), S(32));

            // Pad 3×3
            int cell = S(140);
            int gap = S(6);
            int ox = S(12);
            int oy = S(42);
            int step = cell + gap;
            int x0 = ox, x1 = ox + step, x2 = ox + step * 2;
            int y0 = oy, y1 = oy + step, y2 = oy + step * 2;
            int padW = step * 3 - gap;
            Size cellSize = new Size(cell, cell);

            Btn_K1.Location = new Point(x0, y0);
            Btn_K2.Location = new Point(x2, y0);
            Btn_K3.Location = new Point(x1, y1);
            Btn_K4.Location = new Point(x0, y2);
            Btn_K5.Location = new Point(x2, y2);
            Btn_K1.Size = Btn_K2.Size = Btn_K3.Size = Btn_K4.Size = Btn_K5.Size = cellSize;
            Btn_K1.Enabled = Btn_K2.Enabled = Btn_K3.Enabled = Btn_K4.Enabled = Btn_K5.Enabled = true;
            Btn_K1.Visible = Btn_K2.Visible = Btn_K3.Visible = Btn_K4.Visible = Btn_K5.Visible = true;
            InvalidatePadKeyButtons();

            Point[] fillerPos = new Point[] {
                new Point(x1, y0), new Point(x0, y1), new Point(x2, y1), new Point(x1, y2)
            };
            if (fiveKeyFillers == null)
            {
                fiveKeyFillers = new Panel[fillerPos.Length];
                for (int i = 0; i < fillerPos.Length; i++)
                {
                    Panel p = new Panel();
                    p.BackColor = Color.FromArgb(245, 245, 245);
                    p.BorderStyle = BorderStyle.FixedSingle;
                    p.Enabled = false;
                    this.Controls.Add(p);
                    p.SendToBack();
                    fiveKeyFillers[i] = p;
                }
            }
            for (int i = 0; i < fiveKeyFillers.Length; i++)
            {
                fiveKeyFillers[i].Location = fillerPos[i];
                fiveKeyFillers[i].Size = cellSize;
            }

            // Edit
            groupBox_Button.Location = new Point(ox, y2 + cell + S(10));
            groupBox_Button.Size = new Size(padW, S(130));
            Size editBtn = new Size(padW / 2 - S(24), S(36));
            btn_default.Location = new Point(S(14), S(26));
            Btn_ADJ.Location = new Point(S(14) + padW / 2, S(26));
            btn_Read.Location = new Point(S(14), S(72));
            btn_Save.Location = new Point(S(14) + padW / 2, S(72));
            btn_default.Size = Btn_ADJ.Size = btn_Read.Size = btn_Save.Size = editBtn;

            // Tabs — leave margin so right edge never clips
            int tabX = ox + padW + S(16);
            int tabW = S(DesignW - 12) - tabX; // fill to design right margin
            if (tabW < S(420)) tabW = S(420);
            int tabH = (y2 + cell) - oy + S(10) + groupBox_Button.Height;
            tabControl1.Location = new Point(tabX, oy);
            tabControl1.Size = new Size(tabW, tabH);

            // Mode full width under content
            int modeY = Math.Max(groupBox_Button.Bottom, tabControl1.Bottom) + S(10);
            int modeW = tabX + tabW - ox;
            groupBox_Mode.Location = new Point(ox, modeY);
            groupBox_Mode.Size = new Size(modeW, S(100));

            groupBox_USBMode.Location = new Point(S(10), S(20));
            groupBox_USBMode.Size = new Size(S(240), S(64));
            radioButton_KBS.Location = new Point(S(10), S(26));
            radioButton_HID.Location = new Point(S(115), S(26));

            groupBox2.Location = new Point(S(260), S(20));
            groupBox2.Size = new Size(S(130), S(64));
            radioButton_1P.Location = new Point(S(10), S(26));
            radioButton_2P.Location = new Point(S(65), S(26));

            // Three action buttons packed to the right inside Mode (no clipping)
            int btnW = S(118);
            int btnH = S(42);
            int btnY = S(32);
            int gapBtn = S(10);
            int right = modeW - S(12);
            btnFirmwareUpgrade.Size = Btn_SaveMode.Size = Btn_ReadMode.Size = new Size(btnW, btnH);
            btnFirmwareUpgrade.Location = new Point(right - btnW, btnY);
            Btn_SaveMode.Location = new Point(right - 2 * btnW - gapBtn, btnY);
            Btn_ReadMode.Location = new Point(right - 3 * btnW - 2 * gapBtn, btnY);
            btnFirmwareUpgrade.BringToFront();

            if (btnLangToggle != null)
            {
                btnLangToggle.Size = new Size(S(96), S(28));
                btnLangToggle.Location = new Point(this.ClientSize.Width - S(110), S(8));
                btnLangToggle.BringToFront();
            }
            if (btnThemeToggle != null)
            {
                btnThemeToggle.Size = new Size(S(96), S(28));
                btnThemeToggle.Location = new Point(this.ClientSize.Width - S(220), S(8));
                btnThemeToggle.BringToFront();
            }
            label_Ver.Location = new Point(this.ClientSize.Width - S(70), this.ClientSize.Height - S(22));
        }

        private void LayoutSensitivityColumns()
        {
            // 4 equal columns inside ~480 design-px wide group
            int gw = S(500);
            int gh = S(560);
            groupBox_Sensi.Location = new Point(S(6), S(6));
            groupBox_Sensi.Size = new Size(gw, gh);

            int colW = S(110);
            int baseX = S(24);
            int barH = S(400);
            int trackH = S(430);
            int top = S(28);

            TrackBar[] tracks = new TrackBar[] { trackBar1, trackBar2, trackBar3, trackBar4 };
            ProgressBarWithValue[] bars = new ProgressBarWithValue[] { progressBar1, progressBar2, progressBar3, progressBar4 };
            TextBox[] boxes = new TextBox[] { textBox_V1, textBox_V2, textBox_V3, textBox_V4 };
            Label[] labels = new Label[] { label1, label2, label3, label4 };

            for (int i = 0; i < 4; i++)
            {
                int cx = baseX + i * colW;
                tracks[i].Orientation = Orientation.Vertical;
                tracks[i].Location = new Point(cx, top);
                tracks[i].Size = new Size(S(45), trackH);
                bars[i].Location = new Point(cx + S(48), top + S(12));
                bars[i].Size = new Size(S(36), barH);
                boxes[i].Location = new Point(cx + S(4), top + trackH + S(8));
                boxes[i].Size = new Size(S(64), S(28));
                labels[i].Location = new Point(cx + S(22), top + trackH + S(40));
                labels[i].AutoSize = true;
            }
        }

        private void EnsureFirmwareUpgradeButton()
        {
            if (btnFirmwareUpgrade != null)
                return;
            btnFirmwareUpgrade = new Button();
            btnFirmwareUpgrade.Name = "btnFirmwareUpgrade";
            btnFirmwareUpgrade.BackColor = SystemColors.Control;
            btnFirmwareUpgrade.UseVisualStyleBackColor = false;
            btnFirmwareUpgrade.Click += new EventHandler(this.Btn_OTA_Click);
            this.Controls.Add(btnFirmwareUpgrade);
        }

        private void EnsureLanguageToggleButton()
        {
            if (btnLangToggle != null)
                return;
            btnLangToggle = new Button();
            btnLangToggle.Name = "btnLangToggle";
            btnLangToggle.BackColor = SystemColors.Control;
            btnLangToggle.UseVisualStyleBackColor = false;
            btnLangToggle.Click += new EventHandler(this.Btn_LangToggle_Click);
            this.Controls.Add(btnLangToggle);
        }

        private void EnsureThemeToggleButton()
        {
            if (btnThemeToggle != null)
                return;
            btnThemeToggle = new Button();
            btnThemeToggle.Name = "btnThemeToggle";
            btnThemeToggle.BackColor = SystemColors.Control;
            btnThemeToggle.UseVisualStyleBackColor = false;
            btnThemeToggle.Click += new EventHandler(this.Btn_ThemeToggle_Click);
            this.Controls.Add(btnThemeToggle);
        }

        private void EnsureKeyMappingUi()
        {
            if (tabPage_Map != null)
                return;

            tabPage_Map = new TabPage();
            tabPage_Map.Name = "tabPage_Map";
            tabPage_Map.UseVisualStyleBackColor = true;
            tabControl1.TabPages.Add(tabPage_Map);

            groupBox_Map = new GroupBox();
            groupBox_Map.Location = new Point(S(16), S(20));
            groupBox_Map.Size = new Size(S(480), S(420));
            tabPage_Map.Controls.Add(groupBox_Map);

            label_MapHint = new Label();
            label_MapHint.AutoSize = false;
            label_MapHint.Location = new Point(S(16), S(28));
            label_MapHint.Size = new Size(S(440), S(48));
            groupBox_Map.Controls.Add(label_MapHint);

            label_MapKey = new Label();
            label_MapKey.AutoSize = false;
            label_MapKey.Location = new Point(S(16), S(84));
            label_MapKey.Size = new Size(S(440), S(24));
            groupBox_Map.Controls.Add(label_MapKey);

            string[] names = new string[] { "↖ UL", "↗ UR", "● Center", "↙ DL", "↘ DR" };
            mapKeyCombos = new ComboBox[KEY_COUNT_5KEY];
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
            {
                Label lb = new Label();
                lb.Text = names[i];
                lb.Location = new Point(S(16), S(120) + i * S(42));
                lb.Size = new Size(S(100), S(28));
                groupBox_Map.Controls.Add(lb);

                ComboBox cb = new ComboBox();
                cb.DropDownStyle = ComboBoxStyle.DropDownList;
                cb.Location = new Point(S(130), S(118) + i * S(42));
                cb.Size = new Size(S(200), S(28));
                FillHidUsageCombo(cb);
                int captured = i;
                cb.SelectedIndexChanged += (s, e) =>
                {
                    if (cb.SelectedItem is HidUsageItem item)
                        KeyMappingBuffer[captured] = item.Usage;
                };
                mapKeyCombos[i] = cb;
                groupBox_Map.Controls.Add(cb);
            }

            btn_MapRead = new Button();
            btn_MapRead.Location = new Point(S(16), S(350));
            btn_MapRead.Size = new Size(S(140), S(36));
            btn_MapRead.Click += (s, e) => Read_KeyMapping_Config();
            groupBox_Map.Controls.Add(btn_MapRead);

            btn_MapSave = new Button();
            btn_MapSave.Location = new Point(S(170), S(350));
            btn_MapSave.Size = new Size(S(140), S(36));
            btn_MapSave.Click += (s, e) => Save_KeyMapping_Config();
            groupBox_Map.Controls.Add(btn_MapSave);

            SyncMapCombosFromBuffer();
            radioButton_KBS.CheckedChanged += (s, e) => UpdatePlayerVisibilityForMode();
            radioButton_HID.CheckedChanged += (s, e) => UpdatePlayerVisibilityForMode();
            UpdatePlayerVisibilityForMode();
        }

        private sealed class HidUsageItem
        {
            public int Usage;
            public string Name;
            public override string ToString() { return Name; }
        }

        private void FillHidUsageCombo(ComboBox cb)
        {
            cb.Items.Clear();
            // Common NKRO usages (HID keyboard page)
            string[][] table = new string[][] {
                new[]{"04","A"}, new[]{"05","B"}, new[]{"06","C"}, new[]{"07","D"},
                new[]{"08","E"}, new[]{"09","F"}, new[]{"0A","G"}, new[]{"0B","H"},
                new[]{"0C","I"}, new[]{"0D","J"}, new[]{"0E","K"}, new[]{"0F","L"},
                new[]{"10","M"}, new[]{"11","N"}, new[]{"12","O"}, new[]{"13","P"},
                new[]{"14","Q"}, new[]{"15","R"}, new[]{"16","S"}, new[]{"17","T"},
                new[]{"18","U"}, new[]{"19","V"}, new[]{"1A","W"}, new[]{"1B","X"},
                new[]{"1C","Y"}, new[]{"1D","Z"},
                new[]{"1E","1"}, new[]{"1F","2"}, new[]{"20","3"}, new[]{"21","4"},
                new[]{"22","5"}, new[]{"23","6"}, new[]{"24","7"}, new[]{"25","8"},
                new[]{"26","9"}, new[]{"27","0"},
                new[]{"2C","Space"}, new[]{"28","Enter"}, new[]{"2A","Backspace"},
                new[]{"50","Left"}, new[]{"4F","Right"}, new[]{"52","Up"}, new[]{"51","Down"},
                new[]{"3A","F1"}, new[]{"3B","F2"}, new[]{"3C","F3"},
                new[]{"59","N1"}, new[]{"5A","N2"}, new[]{"5B","N3"}, new[]{"5C","N4"},
                new[]{"5D","N5"}, new[]{"5E","N6"}, new[]{"5F","N7"}, new[]{"60","N8"},
                new[]{"61","N9"}, new[]{"62","N0"},
            };
            foreach (string[] row in table)
            {
                int u = Convert.ToInt32(row[0], 16);
                cb.Items.Add(new HidUsageItem { Usage = u, Name = row[1] + " (0x" + row[0] + ")" });
            }
        }

        private void SyncMapCombosFromBuffer()
        {
            if (mapKeyCombos == null) return;
            for (int i = 0; i < mapKeyCombos.Length; i++)
            {
                ComboBox cb = mapKeyCombos[i];
                int want = KeyMappingBuffer[i];
                int found = -1;
                for (int j = 0; j < cb.Items.Count; j++)
                {
                    HidUsageItem it = cb.Items[j] as HidUsageItem;
                    if (it != null && it.Usage == want) { found = j; break; }
                }
                if (found < 0)
                {
                    cb.Items.Add(new HidUsageItem { Usage = want, Name = "0x" + want.ToString("X02") });
                    found = cb.Items.Count - 1;
                }
                cb.SelectedIndex = found;
            }
        }

        private void UpdatePlayerVisibilityForMode()
        {
            // Keyboard remapping replaces 1P/2P keymap tables; HID mode still needs player.
            bool kbs = radioButton_KBS.Checked;
            groupBox2.Visible = !kbs;
            if (tabPage_Map != null)
                tabPage_Map.Enabled = kbs;
        }

        private void Read_KeyMapping_Config()
        {
            byte ReportSize = 64;
            byte ReportID = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            int index = 0;
            SendByteBuf[index++] = (byte)UserCommand.GET_MAPP;
            SendByteBuf[index++] = 0x45; // E
            SendByteBuf[index++] = 0x41; // A
            SendByteBuf[index++] = 0x44; // D
            SendByteBuf[index++] = 0x00;
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            Thread.Sleep(100);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);
            if (InputReportBuf[ReportIndex] == (byte)UserCommand.GET_MAPP &&
                InputReportBuf[ReportIndex + 1] == 0x52 &&
                InputReportBuf[ReportIndex + 2] == 0x53 &&
                InputReportBuf[ReportIndex + 3] == 0x50)
            {
                for (int i = 0; i < KEY_COUNT_5KEY; i++)
                    KeyMappingBuffer[i] = InputReportBuf[ReportIndex + 5 + i];
                SyncMapCombosFromBuffer();
                MessageBox.Show(bLanguageEN ? "Key map read OK" : "按键映射读取成功", "PADUEX5",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    bLanguageEN
                        ? "Device has no keymap protocol. Flash firmware/grok1.6.bin (OTA or ISP @ 0x08004000), then retry."
                        : "设备暂无按键映射协议。请刷入 firmware/grok1.6.bin（OTA 或 ISP 地址 0x08004000）后重试。",
                    "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Save_KeyMapping_Config()
        {
            byte ReportSize = 64;
            byte ReportID = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            int index = 0;
            SendByteBuf[index++] = (byte)UserCommand.SET_MAPP;
            SendByteBuf[index++] = 0x52; // R
            SendByteBuf[index++] = 0x49; // I
            SendByteBuf[index++] = 0x54; // T
            SendByteBuf[index++] = 0x00;
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
                SendByteBuf[index++] = (byte)KeyMappingBuffer[i];
            // pad to 12 usages like stock keymap length
            for (int i = KEY_COUNT_5KEY; i < 12; i++)
                SendByteBuf[index++] = 0x00;
            byte cs = USBCtrl.UPDATE_CRC(0, SendByteBuf, (byte)index);
            SendByteBuf[index++] = cs;
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            Thread.Sleep(100);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);
            if (InputReportBuf[ReportIndex] == (byte)UserCommand.SET_MAPP &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B)
            {
                MessageBox.Show(bLanguageEN ? "Key map saved" : "按键映射已保存", "PADUEX5",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    bLanguageEN
                        ? "Save failed — flash firmware/grok1.6.bin (see firmware/README.md)."
                        : "保存失败：请刷入 firmware/grok1.6.bin（见 firmware/README.md）。",
                    "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Btn_ThemeToggle_Click(object sender, EventArgs e)
        {
            bDarkMode = !bDarkMode;
            ApplyTheme();
            UpdateThemeToggleText();
            WriteThemeToConfig();
        }

        private void UpdateThemeToggleText()
        {
            if (btnThemeToggle == null)
                return;
            if (bLanguageEN)
                btnThemeToggle.Text = bDarkMode ? "Light" : "Dark";
            else
                btnThemeToggle.Text = bDarkMode ? "白天" : "夜间";
        }

        private void WriteThemeToConfig()
        {
            try
            {
                string path = Directory.GetCurrentDirectory() + "\\Config.ini";
                if (!File.Exists(path))
                    return;
                string[] lines = File.ReadAllLines(path);
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("THEME=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "THEME=" + (bDarkMode ? "DARK" : "LIGHT");
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Array.Resize(ref lines, lines.Length + 1);
                    lines[lines.Length - 1] = "THEME=" + (bDarkMode ? "DARK" : "LIGHT");
                }
                File.WriteAllLines(path, lines);
            }
            catch { }
        }

        private void ApplyTheme()
        {
            Color formBg = bDarkMode ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color panelBg = bDarkMode ? Color.FromArgb(55, 55, 58) : SystemColors.Control;
            Color text = bDarkMode ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
            Color filler = bDarkMode ? Color.FromArgb(50, 50, 52) : Color.FromArgb(245, 245, 245);

            this.BackColor = formBg;
            tabControl1.BackColor = panelBg;
            tabPage_Sensi.BackColor = tabPage_Color.BackColor = tabPage_DTick.BackColor = panelBg;
            groupBox_Button.BackColor = groupBox_Mode.BackColor = groupBox_Sensi.BackColor =
                groupBox_Color.BackColor = groupBox_DTick.BackColor = groupBox_USBMode.BackColor =
                groupBox2.BackColor = panelBg;
            groupBox_Button.ForeColor = groupBox_Mode.ForeColor = groupBox_Sensi.ForeColor =
                groupBox_Color.ForeColor = groupBox_DTick.ForeColor = groupBox_USBMode.ForeColor =
                groupBox2.ForeColor = text;
            tabControl1.ForeColor = text;

            if (fiveKeyFillers != null)
            {
                for (int i = 0; i < fiveKeyFillers.Length; i++)
                    fiveKeyFillers[i].BackColor = filler;
            }

            if (btnLangToggle != null)
            {
                btnLangToggle.ForeColor = text;
                btnLangToggle.BackColor = panelBg;
            }
            if (btnThemeToggle != null)
            {
                btnThemeToggle.ForeColor = text;
                btnThemeToggle.BackColor = panelBg;
            }

            UpdateThemeToggleText();
            InvalidatePadKeyButtons();
        }

        private void EnsurePadKeyVectorPaint()
        {
            if (padKeyVectorWired)
                return;
            PadKeyPainter.WireButton(Btn_K1, PadKeyPainter.PadKeyKind.UpperLeft);
            PadKeyPainter.WireButton(Btn_K2, PadKeyPainter.PadKeyKind.UpperRight);
            PadKeyPainter.WireButton(Btn_K3, PadKeyPainter.PadKeyKind.Center);
            PadKeyPainter.WireButton(Btn_K4, PadKeyPainter.PadKeyKind.DownLeft);
            PadKeyPainter.WireButton(Btn_K5, PadKeyPainter.PadKeyKind.DownRight);
            padKeyVectorWired = true;
        }

        internal PadKeyPainter.PadKeyPaintArgs GetPadKeyPaintArgs(PadKeyPainter.PadKeyKind kind)
        {
            int key = (int)kind;
            int selected = SensorIndex / SENSORS_PER_KEY;
            Color cellBg = bDarkMode ? Color.FromArgb(64, 64, 64) : SystemColors.ButtonFace;
            Color outline = bDarkMode ? Color.FromArgb(230, 230, 230) : Color.Black;
            bool triggered = key >= 0 && key < padKeyActive.Length && padKeyActive[key];
            Color triggerFill = GetPadLedColor(key);
            if (triggered)
                triggerFill = Color.FromArgb(220, triggerFill);
            return new PadKeyPainter.PadKeyPaintArgs
            {
                CellBackground = cellBg,
                OutlineColor = outline,
                Selected = key == selected,
                Triggered = triggered,
                TriggerFill = triggerFill
            };
        }

        private Color GetPadLedColor(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= KEY_COUNT_5KEY)
                return Color.White;
            int led = ColorBuffer[keyIndex * SENSORS_PER_KEY];
            return LedCodeToColor(led);
        }

        private Color LedCodeToColor(int led)
        {
            if (led == LED_COLOR_R) return Color.Red;
            if (led == LED_COLOR_G) return Color.LimeGreen;
            if (led == LED_COLOR_B) return Color.RoyalBlue;
            if (led == LED_COLOR_Y) return Color.Gold;
            if (led == LED_COLOR_P) return Color.Fuchsia;
            if (led == LED_COLOR_C) return Color.Cyan;
            if (led == LED_COLOR_W) return Color.White;
            return Color.White;
        }

        private void UpdatePadTriggerFromReport(byte[] buf, int reportIndex)
        {
            bool[] next = new bool[KEY_COUNT_5KEY];

            if (buf[reportIndex + 0] == (byte)UserCommand.SET_MRAW &&
                buf[reportIndex + 1] == 0x4D &&
                buf[reportIndex + 2] == 0x52 &&
                buf[reportIndex + 3] == 0x41 &&
                buf[reportIndex + 4] == 0x57)
            {
                int keyCode = (buf[reportIndex + 5] << 8) | buf[reportIndex + 6];
                for (int k = 0; k < KEY_COUNT_5KEY; k++)
                {
                    if ((keyCode & (1 << k)) != 0)
                        next[k] = true;
                }

                for (int k = 0; k < KEY_COUNT_5KEY; k++)
                {
                    if (next[k])
                        continue;
                    int baseIdx = k * SENSORS_PER_KEY;
                    for (int s = 0; s < SENSORS_PER_KEY; s++)
                    {
                        int idx = baseIdx + s;
                        int raw = (buf[reportIndex + 8 + idx * 2] << 8) | buf[reportIndex + 8 + idx * 2 + 1];
                        if (raw >= SensiBuffer[idx])
                            next[k] = true;
                    }
                }
            }
            else if (bDeviceStatusFG)
            {
                int mask = buf[reportIndex + 1];
                if (mask == 0)
                    mask = buf[reportIndex + 2];
                for (int k = 0; k < KEY_COUNT_5KEY; k++)
                {
                    if ((mask & (1 << k)) != 0)
                        next[k] = true;
                }
            }

            bool changed = false;
            for (int k = 0; k < KEY_COUNT_5KEY; k++)
            {
                if (padKeyActive[k] != next[k])
                {
                    padKeyActive[k] = next[k];
                    changed = true;
                }
            }
            if (changed)
                InvalidatePadKeyButtons();
        }

        private void PollPadInputState()
        {
            if (!bDeviceStatusFG)
                return;
            byte reportSize = 64;
            byte reportId = 0;
            byte reportIndex = 0;
            byte[] inputReportBuf = new byte[64];
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, reportId, reportSize, inputReportBuf);
            UpdatePadTriggerFromReport(inputReportBuf, reportIndex);
        }

        private void InvalidatePadKeyButtons()
        {
            Btn_K1.Invalidate();
            Btn_K2.Invalidate();
            Btn_K3.Invalidate();
            Btn_K4.Invalidate();
            Btn_K5.Invalidate();
        }

        private void Btn_LangToggle_Click(object sender, EventArgs e)
        {
            bLanguageEN = !bLanguageEN;
            ApplyUiLanguage();
        }

        private void ApplyUiLanguage()
        {
            if (bLanguageEN)
            {
                if (btnLangToggle != null) btnLangToggle.Text = "中文";
                btn_default.Text = "Default";
                btn_Read.Text = "Read";
                btn_Save.Text = "Save";
                Btn_ADJ.Text = "Debug";
                Btn_ReadMode.Text = "Read Mode";
                Btn_SaveMode.Text = "Save Mode";
                if (btnFirmwareUpgrade != null) btnFirmwareUpgrade.Text = "FW Upgrade";
                groupBox_Button.Text = "Edit";
                groupBox_Mode.Text = "Mode";
                groupBox_Sensi.Text = "Sensitivity";
                groupBox_Color.Text = "Color";
                groupBox_DTick.Text = "Debounce (0~1000 ms)";
                label_DTick.Text = "Hold (ms) =";
                tabPage_Sensi.Text = "Sensitivity";
                tabPage_Color.Text = "Color";
                tabPage_DTick.Text = "Debounce";
                groupBox_USBMode.Text = "USB";
                radioButton_KBS.Text = "KeyBoard";
                radioButton_HID.Text = "HID Mode";
                if (tabPage_Map != null) tabPage_Map.Text = "Key Map";
                if (groupBox_Map != null) groupBox_Map.Text = "Keyboard mapping";
                if (label_MapHint != null) label_MapHint.Text = "Keyboard mode only. Requires firmware grok1.6 has no remap (grok1.0-1.5 are unsafe). Remap replaces 1P/2P; HID mode still uses 1P/2P.";
                if (label_MapKey != null) label_MapKey.Text = "Per-pad HID usage (select then Save Map):";
                if (btn_MapRead != null) btn_MapRead.Text = "Read Map";
                if (btn_MapSave != null) btn_MapSave.Text = "Save Map";
            }
            else
            {
                if (btnLangToggle != null) btnLangToggle.Text = "English";
                btn_default.Text = "默认设置";
                btn_Read.Text = "读取设置";
                btn_Save.Text = "保存设置";
                Btn_ReadMode.Text = "读取模式";
                Btn_SaveMode.Text = "保存模式";
                if (btnFirmwareUpgrade != null) btnFirmwareUpgrade.Text = "固件升级";
                groupBox_Button.Text = "Edit";
                groupBox_Mode.Text = "模式";
                groupBox_Sensi.Text = "灵敏度";
                groupBox_Color.Text = "调色";
                groupBox_DTick.Text = "延迟释放 (0~1000 ms)";
                label_DTick.Text = "松开保持(ms) =";
                tabPage_Sensi.Text = "灵敏度";
                tabPage_Color.Text = "调色";
                tabPage_DTick.Text = "延迟释放";
                groupBox_USBMode.Text = "USB";
                radioButton_KBS.Text = "键盘";
                radioButton_HID.Text = "手柄";
                if (tabPage_Map != null) tabPage_Map.Text = "按键映射";
                if (groupBox_Map != null) groupBox_Map.Text = "键盘按键映射";
                if (label_MapHint != null) label_MapHint.Text = "仅键盘模式，需固件 grok1.6 暂无改键（grok1.0～1.5 禁止）。改键后不必再分 1P/2P；手柄模式仍用 1P/2P。";
                if (label_MapKey != null) label_MapKey.Text = "五个踏板对应 HID 键值（选好后点保存映射）：";
                if (btn_MapRead != null) btn_MapRead.Text = "读取映射";
                if (btn_MapSave != null) btn_MapSave.Text = "保存映射";
            }
            UpdateDebugButtonText();
            UpdateThemeToggleText();
        }

        private void UpdateDebugButtonText()
        {
            if (bLanguageEN)
                Btn_ADJ.Text = bDebugModesFG ? "Stop" : "Debug";
            else
                Btn_ADJ.Text = bDebugModesFG ? "停止" : "调试模式";
        }

        private void SendRawModeCommand(byte b0, byte b1, byte b2, byte b3, byte b4)
        {
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            byte[] SendByteBuf = new byte[64];
            SendByteBuf[index++] = (byte)UserCommand.SET_MRAW;
            SendByteBuf[index++] = b0;
            SendByteBuf[index++] = b1;
            SendByteBuf[index++] = b2;
            SendByteBuf[index++] = b3;
            SendByteBuf[index++] = b4;
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
        }

        private void SetDeviceRawDebugMode(bool enable)
        {
            if (enable)
                SendRawModeCommand(0x52, 0x41, 0x57, 0x4D, 0x44); // RAWMD
            else
                SendRawModeCommand(0x4E, 0x4F, 0x4C, 0x4D, 0x44); // NOLMD
            bDebugModesFG = enable;
            UpdateDebugButtonText();
        }

        /// <summary>FW rejects config R/W while RAW debug is active — pause and restore.</summary>
        private byte Read_Config_PausingDebug(byte configType, int[] configOut, int len)
        {
            bool wasDebug = bDebugModesFG;
            if (wasDebug)
            {
                SetDeviceRawDebugMode(false);
                Thread.Sleep(80);
            }
            byte result = Read_Config_Value(configType, configOut, len);
            if (wasDebug)
            {
                Thread.Sleep(80);
                SetDeviceRawDebugMode(true);
            }
            return result;
        }

        private byte Write_Config_PausingDebug(byte configType, int[] configOut, int len)
        {
            bool wasDebug = bDebugModesFG;
            if (wasDebug)
            {
                SetDeviceRawDebugMode(false);
                Thread.Sleep(80);
            }
            byte result = Write_Config_Value(configType, configOut, len);
            if (wasDebug)
            {
                Thread.Sleep(80);
                SetDeviceRawDebugMode(true);
            }
            return result;
        }

        /// =============================================================================================
        /// ==== Timer Tick ====
        /// =============================================================================================
        /// 
        private void timer1_Tick(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            if (bDeviceStatusFG)
            {
                if (bDebugModesFG)
                    Seneor_Raw_Data_Read_and_Display();
                else
                    PollPadInputState();
            }
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Read Config ====
        /// =============================================================================================
        ///
        private void ReadAppConfig()
        {
            ///------------------------------------------------------------------------------------------
            string Currentpath = Directory.GetCurrentDirectory() + "\\Config.ini";
            string Str = "";
            ///------------------------------------------------------------------------------------------
            AppName = "DDR Config";
            ///------------------------------------------------------------------------------------------
            using (StreamReader Rhandle = File.OpenText(Currentpath))         // Create a file to write to.
            {
                for (byte i = 0; i < 5; i++)
                {
                    Str = Rhandle.ReadLine();
                    if (Str == "[APP Config]")
                    {
                        for (byte j = 0; j < 50; j++)
                        {
                            Str = Rhandle.ReadLine();
                            string[] S = Str.Split(';');
                            Str = S[0];
                            string[] StrTab = Str.Split('=');
                            Str = StrTab[0];
                            if (Str == "")
                            {
                                break;
                            }
                            else if (Str == "NAME")
                            {
                                AppName = StrTab[1];
                            }
                            else if (Str == "LANGUAGE")
                            {
                                bLanguageEN = (StrTab[1] == "EN");
                            }
                            else if (Str == "THEME")
                            {
                                bDarkMode = (StrTab[1] == "DARK");
                            }
                            else if (Str == "UPDATE")
                            {
                            }
                            else if (Str == "CONFIGEND")
                            {
                                if (StrTab[1] == "YES") break;
                            }
                        }
                    }
                }
            }
            ///------------------------------------------------------------------------------------------
            this.Text = AppName;
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Form Load ====
        /// =============================================================================================
        ///
        private void DDR_Form_Load(object sender, EventArgs e)
        {
            //Find_USB_Deivce();
        }

        /// =============================================================================================
        /// ==== Form Closing  ====
        /// =============================================================================================
        ///
        private void DDR_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            USBCtrl.USBLib_Disconnect();
            //this.Close();
            //App_Main main = new App_Main();
            //main.Show();
        }
        /// =============================================================================================
        /// ==== HID Event Changed  ====
        /// =============================================================================================
        ///
        private void HID_Event_Changed_Delegate(object sender, HIDLibraryLibChangedEventArgs args)
        {
            BeginInvoke(new UpdateUSBHIDStateDelegate(UpdateHidLibraryState), args);
        }
        /// =============================================================================================
        /// ==== Display All ====
        /// =============================================================================================
        ///
        private void UpdateHidLibraryState(HIDLibraryLibChangedEventArgs args)
        {
            HIDLibraryState mHIDLibraryState = args.HIDLibraryState;
            switch (mHIDLibraryState.DeviceType)    //V1.0.0.16:20240921(1)Change DDR 1P and 2P Mode
            {
                case DeviceType.DDR_KBS_PID1: break;
                case DeviceType.DDR_KBS_PID2: break;
                case DeviceType.DDR_HID_PID1: break;
                case DeviceType.DDR_HID_PID2: break;
                default:break;
            }
        }

        /// =============================================================================================
        /// ==== Find USB Connected ====
        /// =============================================================================================
        ///
        private void Find_USB_Deivce()
        {
            //-------------------------------------------------------------------------------------------
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //-------------------------------------------------------------------------------------------
            this.Text = AppName;
            label_Ver.Text = "";
            groupBox_Mode.Enabled = false;
            radioButton_1P.Checked = false;
            radioButton_1P.Enabled = false;  //V1.0.0.25:20250402(1)DDR Game Pad Mode
            radioButton_2P.Checked = false;
            radioButton_2P.Enabled = false;  //V1.0.0.25:20250402(1)DDR Game Pad Mode
            bDeviceStatusFG = false;
            //-------------------------------------------------------------------------------------------//V1.0.0.20:20241231(1)Display OTA Device infor on App Title
            if (USBCtrl.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                this.Text = AppName + " ( OTA Device Connected)";
                return;
            }
            //-------------------------------------------------------------------------------------------
            if ((USBCtrl.USBLib_Connect((int)DeviceType.DDR_KBS_VID, (int)DeviceType.DDR_KBS_PID1, (int)DeviceType.None) != (int)DeviceType.None) ||
                (USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.DDR_HID_PID1, (int)DeviceType.None) != (int)DeviceType.None))      //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
            {
                bDeviceStatusFG = true;
                radioButton_1P.Enabled = true;
            }
            //-------------------------------------------------------------------------------------------
            if ((USBCtrl.USBLib_Connect((int)DeviceType.DDR_KBS_VID, (int)DeviceType.None, (int)DeviceType.DDR_KBS_PID2) != (int)DeviceType.None) ||
                (USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.None, (int)DeviceType.DDR_HID_PID2) != (int)DeviceType.None) ||    //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
                (USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.None, (int)DeviceType.DDR_HID_PID3) != (int)DeviceType.None))      //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
            {
                bDeviceStatusFG = true;
                radioButton_2P.Enabled = true;
            }
            //-------------------------------------------------------------------------------------------
            if (bDeviceStatusFG == true)
            {
                this.Text = AppName + " (Device Connected)";
                groupBox_Mode.Enabled = true;
                //---------------------------------------------------------------------------------------
                if (radioButton_1P.Enabled == true)
                {
                    mDeviceIndex = 0;
                    radioButton_1P.Checked = true;
                    radioButton_2P.Checked = false;
                }
                else if (radioButton_2P.Enabled == true)
                {
                    mDeviceIndex = 1;
                    radioButton_1P.Checked = false;
                    radioButton_2P.Checked = true;
                }
                else
                {
                    mDeviceIndex = 0;
                    radioButton_1P.Checked = false;
                    radioButton_2P.Checked = false;
                }
                //---------------------------------------------------------------------------------------
                Btn_ReadMode_Click(null, null);
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
            else
            {
                USBCtrl.USBLib_Disconnect();
            }
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== USB First Connected ====
        /// =============================================================================================
        ///
        /*
        private void DeivceFirstConnected()
        {
            if (bDeviceStatusFG == true)
            {
                this.Text = AppName + " (Device Connected)";
                //this.groupBox_Mode.Enabled = true;
                groupBox_Mode.Enabled = true;
                //-------------------------------------------------------------------------------------------
                if (USBCtrl.DDR_GetDeviceType(0) == (int)DeviceType.DDR_KBS_PID1 ||
                    USBCtrl.DDR_GetDeviceType(0) == (int)DeviceType.DDR_HID_PID1)
                {
                    //this.Text = AppName + " (Device Connected KBS 1P)";
                    radioButton_1P.Enabled = true;
                }
                else
                {
                    radioButton_1P.Enabled = false;
                }
                //-------------------------------------------------------------------------------------------
                if (USBCtrl.DDR_GetDeviceType(1) == (int)DeviceType.DDR_KBS_PID2 ||
                    USBCtrl.DDR_GetDeviceType(1) == (int)DeviceType.DDR_HID_PID2)
                {
                    //this.Text = AppName + " (Device Connected KBS 2P)";
                    radioButton_2P.Enabled = true;
                }
                else
                {
                    //this.Text = AppName + " (Device Connected HID 2P)";
                    radioButton_2P.Enabled = false;
                }
                //-------------------------------------------------------------------------------------------
                if (radioButton_1P.Enabled == true)
                {
                    mDeviceIndex = 0;
                    radioButton_1P.Checked = true;
                    radioButton_2P.Checked = false;
                }
                else if (radioButton_2P.Enabled == true)
                {
                    mDeviceIndex = 1;
                    radioButton_1P.Checked = false;
                    radioButton_2P.Checked = true;
                }
                else
                {
                    mDeviceIndex = 0;
                    radioButton_1P.Checked = false;
                    radioButton_2P.Checked = false;
                }
                //-------------------------------------------------------------------------------------------
                Btn_ReadMode_Click(null, null);
            }
            else
            {
                this.Text = AppName;
                //this.groupBox_Mode.Enabled = false;
                groupBox_Mode.Enabled = false;
                radioButton_1P.Checked = false;
                radioButton_2P.Checked = false;
            }
        }*/



        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        private const int LED_COLOR_R = 0x01;   //R
        private const int LED_COLOR_G = 0x02;   //G
        private const int LED_COLOR_B = 0x04;   //B
        private const int LED_COLOR_Y = 0x03;   //R+G
        private const int LED_COLOR_P = 0x05;   //R+B
        private const int LED_COLOR_C = 0x06;   //G+B
        private const int LED_COLOR_W = 0x07;   //R+G+B

        // 5Key only: 5 pads × 4 sensors (align FW DATA_LEN = 5*4*2)
        private const int ADC_MAX_CHAN  = CONFIG_VALUE_COUNT;
        private const int DEFAULT_SENSI = 200;//800;  //V1.0.0.25:20250402(2)Default Seni change from 800 to 200
        private const int MAX_VAL_SENSI = 4095;
        // Pedal LED palette limited to Red / Blue / White
        private const int DEFAULT_COLOR  = LED_COLOR_W;
        private const int DEFAULT_COLOR1 = LED_COLOR_R; // Key1 ↖ red
        private const int DEFAULT_COLOR2 = LED_COLOR_R; // Key2 ↗ red
        private const int DEFAULT_COLOR3 = LED_COLOR_W; // Key3 center white
        private const int DEFAULT_COLOR4 = LED_COLOR_B; // Key4 ↙ blue
        private const int DEFAULT_COLOR5 = LED_COLOR_B; // Key5 ↘ blue

        private const int DEFAULT_DTICK = 0x03;

        private int SensorIndex = 0;
        private int[] SensiBuffer = new int[CONFIG_VALUE_COUNT];
        private int[] ColorBuffer = new int[CONFIG_VALUE_COUNT];
        private int[] DTickBuffer = new int[CONFIG_VALUE_COUNT];

        /// <summary>
        /// FW V2.12 (and current tree) applies debounce as DTick[key] via:
        ///   Tck[i/4] = DTick[i/4]
        /// NOT DTick[key*4]. Host mirrors that layout so save/feel match on V2.12.
        /// When firmware is fixed to DTick[(i/4)*4], switch GetDebounceKeyIndex back.
        /// </summary>
        private int GetDebounceKeyIndex()
        {
            int key = SensorIndex / 4;
            if (key < 0) key = 0;
            if (key >= KEY_COUNT_5KEY) key = KEY_COUNT_5KEY - 1;
            return key; // FW quirk: one slot per key at index 0..4
        }

        private int GetDebounceValue()
        {
            return DTickBuffer[GetDebounceKeyIndex()];
        }

        private void SetDebounceValue(int ms)
        {
            if (ms < 0) ms = 0;
            if (ms > 1000) ms = 1000;
            int key = GetDebounceKeyIndex();
            // grok1.0.bin + fixed source: DTick[(i/4)*4]; V2.12 quirk: DTick[key].
            // Write both layouts so save works on either firmware.
            DTickBuffer[key] = ms;
            int packed = key * SENSORS_PER_KEY;
            if (packed >= 0 && packed < CONFIG_VALUE_COUNT)
                DTickBuffer[packed] = ms;
        }

        readonly int[] DEFAULT_SENSI_TAB = new int[]//5*4
        {
          DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI, //  0..3  : Key1 UL
          DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI, //  4..7  : Key2 UR
          DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI, //  8..11 : Key3 Center
          DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI, // 12..15 : Key4 DL
          DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI,DEFAULT_SENSI, // 16..19 : Key5 DR
        };

        readonly int[] DEFAULT_COLOR_TAB = new int[]//5*4
        {
          DEFAULT_COLOR1,DEFAULT_COLOR1,DEFAULT_COLOR1,DEFAULT_COLOR1, // Key1
          DEFAULT_COLOR2,DEFAULT_COLOR2,DEFAULT_COLOR2,DEFAULT_COLOR2, // Key2
          DEFAULT_COLOR3,DEFAULT_COLOR3,DEFAULT_COLOR3,DEFAULT_COLOR3, // Key3
          DEFAULT_COLOR4,DEFAULT_COLOR4,DEFAULT_COLOR4,DEFAULT_COLOR4, // Key4
          DEFAULT_COLOR5,DEFAULT_COLOR5,DEFAULT_COLOR5,DEFAULT_COLOR5, // Key5
        };
        // Debounce defaults: indices 0..4 = Key1..Key5 (match FW DTick[i/4]); rest unused
        readonly int[] DEFAULT_DTICK_TAB = new int[]//5*4 packet, FW uses [0..4]
        {
          DEFAULT_DTICK, // 0 Key1
          DEFAULT_DTICK, // 1 Key2
          DEFAULT_DTICK, // 2 Key3
          DEFAULT_DTICK, // 3 Key4
          DEFAULT_DTICK, // 4 Key5
          DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK, // 5..7 unused by FW quirk
          DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK, // 8..11
          DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK, // 12..15
          DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK,DEFAULT_DTICK, // 16..19
        };

        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        private void Load_Default_Sens()
        {
            for (int i = 0; i < CONFIG_VALUE_COUNT; i++)
            {
                SensiBuffer[i] = DEFAULT_SENSI_TAB[i];
                ColorBuffer[i] = DEFAULT_COLOR_TAB[i];
                DTickBuffer[i] = DEFAULT_DTICK_TAB[i];
            }
        }
        /// =============================================================================================
        /// ==== Read  Config Value ====
        /// =============================================================================================
        ///
        //CMD : 52 45 41 44 00
        //RSP : 52 52 53 50 00
        //Data: 03 E8 03 E8 03 E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 
        //CS  : 66
        private byte Read_Config_Value(byte ConfigType, int[] ConfigOut, int Len)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_SENS);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            //-------------------------------------------------------------------------------------------
            //ConfigType:   Bit[7:4] : 0x00=WMCU,  0x10=ExChip1, 0x20=ExChip2
            //              Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x020=DTick 
            SendByteBuf[index++] = (byte)(ConfigType);
            //USBCtrl.DDR_USBDeviceCtrlOutput(mDeviceIndex, SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlInput(mDeviceIndex, InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x52 &&
                InputReportBuf[ReportIndex + 2] == 0x53 &&
                InputReportBuf[ReportIndex + 3] == 0x50 &&
                InputReportBuf[ReportIndex + 4] == (byte)(ConfigType))
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64 - ReportIndex; i++)
                {
                    RxStr += InputReportBuf[ReportIndex + i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
                byte SyncLen = 5;
                byte DataLen = (byte)(CONFIG_VALUE_COUNT * 2); // 5Key: 20*2 — FW DATA_LEN
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = USBCtrl.UPDATE_CRC(ReportIndex,InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[ReportIndex + PktLen - 1])
                {
                    // 5Key FW packs all 5 keys in one WMCU packet (ConfigType high nibble 0x00)
                    byte ConfigOutIndex = 0;

                    for (int i = 0; i < Len; i++)
                    {
                        ConfigOut[ConfigOutIndex + i] = ((InputReportBuf[ReportIndex + SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[ReportIndex + SyncLen + (i * 2 + 1)]));
                    }
                }
                else
                {
                    MessageBox.Show("Read Config CS Error !!! (" + CS.ToString("X02") +","+ InputReportBuf[ReportIndex + PktLen - 1].ToString("X02") + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 0;
                }
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Read Config Error !!!(" 
                    + InputReportBuf[ReportIndex + 0].ToString("X02")
                    + InputReportBuf[ReportIndex + 1].ToString("X02")
                    + InputReportBuf[ReportIndex + 2].ToString("X02")
                    + InputReportBuf[ReportIndex + 3].ToString("X02")
                    + InputReportBuf[ReportIndex + 4].ToString("X02")+")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Write  Config Value ====
        /// =============================================================================================
        ///
        //CMD : 57 52 49 54 00
        //Data: 03 E8 03 E8 03 E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 
        //CS  : FA
        //RSP : 57 4F 4B FA 00
        private byte Write_Config_Value(byte ConfigType, int[] ConfigOut,int Len)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            byte Txd_CS = 0;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_SENS);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            //-------------------------------------------------------------------------------------------
            //ConfigType:   Bit[7:4] : 0x00=WMCU (5Key: all keys in one packet)
            //              Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick 
            SendByteBuf[index++] = (byte)(ConfigType);
            //-------------------------------------------------------------------------------------------
            byte ConfigOutIndex = 0; // 5Key: always from buffer start
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < Len; i++)
            {
                SendByteBuf[index++] = (byte)((ConfigOut[ConfigOutIndex + i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((ConfigOut[ConfigOutIndex + i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            Txd_CS = USBCtrl.UPDATE_CRC(0,SendByteBuf, index);
            SendByteBuf[index++] = Txd_CS;
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlOutput(mDeviceIndex, SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlInput(mDeviceIndex, InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B &&
                InputReportBuf[ReportIndex + 4] == (byte)(ConfigType))
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64-ReportIndex; i++)
                {
                    RxStr += InputReportBuf[ReportIndex + i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Write Config Error !!!("
                    + InputReportBuf[ReportIndex + 0].ToString("X02")
                    + InputReportBuf[ReportIndex + 1].ToString("X02")
                    + InputReportBuf[ReportIndex + 2].ToString("X02")
                    + InputReportBuf[ReportIndex + 3].ToString("X02")
                    + InputReportBuf[ReportIndex + 4].ToString("X02") + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            MessageBox.Show("Write Config Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Sensor RAW Data ReFlash  ====
        /// =============================================================================================
        ///
        private void Seneor_Raw_Data_Read_and_Display()
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            UInt16[] RawBuffer = new UInt16[64];
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlInput(mDeviceIndex, InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_MRAW &&
                InputReportBuf[ReportIndex + 1] == 0x4D &&  //M
                InputReportBuf[ReportIndex + 2] == 0x52 &&  //R
                InputReportBuf[ReportIndex + 3] == 0x41 &&  //A
                InputReportBuf[ReportIndex + 4] == 0x57)    //W
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64 - ReportIndex; i++)
                {
                    RxStr += InputReportBuf[ReportIndex + i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
                //InputReportBuf[ReportIndex + 5] = (AppUser.KeyCode >> 8) & 0xff;
                //InputReportBuf[ReportIndex + 6] = (AppUser.KeyCode >> 0) & 0xff;
                //InputReportBuf[ReportIndex + 7] = 0xAA;
                //---------------------------------------------------------------------------------------
                for (int i = 0; i < 16; i++)
                {
                    RawBuffer[i] = (UInt16)((InputReportBuf[ReportIndex + 8 + i * 2 + 0] << 8) | (InputReportBuf[ReportIndex + 8 + i * 2 + 1]));
                }
                //---------------------------------------------------------------------------------------
                int SenIndex = (SensorIndex / 4) * 4;
                progressBar1.Value = RawBuffer[SenIndex + 0];
                progressBar2.Value = RawBuffer[SenIndex + 1];
                progressBar3.Value = RawBuffer[SenIndex + 2];
                progressBar4.Value = RawBuffer[SenIndex + 3];
                //---------------------------------------------------------------------------------------
                UpdatePadTriggerFromReport(InputReportBuf, ReportIndex);
            }
        }



        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  Btn Sensi K1-K9 Click  ====
        /// =============================================================================================
        ///
        private void Sensor_Panel_Display_ReFlash()
        {
            //-------------------------------------------------------------------------------------------
            InvalidatePadKeyButtons();
            //-------------------------------------------------------------------------------------------
            groupBox_Button.Enabled = true;
            groupBox_Sensi.Enabled = true;
            groupBox_Color.Enabled = true;
            groupBox_DTick.Enabled = true;
            //-------------------------------------------------------------------------------------------
            if (tabControl1.SelectedIndex == 0)
            {
                //---------------------------------------------------------------------------------------
                progressBar1.Value = 0;
                progressBar2.Value = 0;
                progressBar3.Value = 0;
                progressBar4.Value = 0;
                //---------------------------------------------------------------------------------------
                int SenIndex = (SensorIndex / 4) * 4;
                int SensVal = 0;
                //double Percent = 0.0;

                SensVal = SensiBuffer[SenIndex + 0];
                //Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV1.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                textBox_V1.Text = SensVal.ToString();
                trackBar1.Value = SensVal;

                SensVal = SensiBuffer[SenIndex + 1];
                //Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV2.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                textBox_V2.Text = SensVal.ToString();
                trackBar2.Value = SensVal;

                SensVal = SensiBuffer[SenIndex + 2];
                //Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV3.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                textBox_V3.Text = SensVal.ToString();
                trackBar3.Value = SensVal;

                SensVal = SensiBuffer[SenIndex + 3];
                //Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV4.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                textBox_V4.Text = SensVal.ToString();
                trackBar4.Value = SensVal;
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 1)
            {
                //---------------------------------------------------------------------------------------
                if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_R) { Btn_LED.BackColor = System.Drawing.Color.Red; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_G) { Btn_LED.BackColor = System.Drawing.Color.Green; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_B) { Btn_LED.BackColor = System.Drawing.Color.Blue; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_Y) { Btn_LED.BackColor = System.Drawing.Color.Yellow; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_P) { Btn_LED.BackColor = System.Drawing.Color.Fuchsia; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_C) { Btn_LED.BackColor = System.Drawing.Color.Cyan; }
                else if (ColorBuffer[SensorIndex] == (byte)LED_COLOR_W) { Btn_LED.BackColor = System.Drawing.Color.White; }
                //---------------------------------------------------------------------------------------
            }
            else if (tabControl1.SelectedIndex == 2)
            {
                //---------------------------------------------------------------------------------------
                // FW V2.12: effective slot is DTick[key], not DTick[key*4]
                int SensVal = GetDebounceValue();
                if (SensVal < trackBar_DTick.Minimum) SensVal = trackBar_DTick.Minimum;
                if (SensVal > trackBar_DTick.Maximum) SensVal = trackBar_DTick.Maximum;
                textBox_DTick.Text = SensVal.ToString();
                trackBar_DTick.Value = SensVal;
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
        }


        /// =============================================================================================
        /// ==== Sens Value changed  ====
        /// =============================================================================================
        private void Btn_K1_Click(object sender, EventArgs e)
        {
            SensorIndex = (1 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K2_Click(object sender, EventArgs e)
        {
            SensorIndex = (2 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K3_Click(object sender, EventArgs e)
        {
            SensorIndex = (3 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K4_Click(object sender, EventArgs e)
        {
            SensorIndex = (4 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K5_Click(object sender, EventArgs e)
        {
            SensorIndex = (5 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K6_Click(object sender, EventArgs e)
        {
            SensorIndex = (6 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K7_Click(object sender, EventArgs e)
        {
            SensorIndex = (7 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K8_Click(object sender, EventArgs e)
        {
            SensorIndex = (8 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_K9_Click(object sender, EventArgs e)
        {
            SensorIndex = (9 - 1) * 4;
            //tabControl1.SelectedIndex = 0;
            Sensor_Panel_Display_ReFlash();
        }

        /// =============================================================================================
        /// ====  Sensor RAW Config  ====
        /// =============================================================================================
        ///
        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            TabControl tabControl = (TabControl)sender;
            TabPage selectedTab = tabControl.SelectedTab;//tabControl1
            Console.WriteLine("Selected tab: " + tabControl.SelectedTab.Name + ",Index: " + tabControl1.SelectedIndex);
            // 在这里添加你的逻辑
            Sensor_Panel_Display_ReFlash();
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar1.Value;
            int SenIndex = (SensorIndex / 4) * 4;
            //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));//Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
            textBox_V1.Text = SensVal.ToString();
            SensiBuffer[SenIndex + 0] = SensVal;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar2.Value;
            int SenIndex = (SensorIndex / 4) * 4;
            //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV2.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
            textBox_V2.Text = SensVal.ToString();
            SensiBuffer[SenIndex + 1] = SensVal;
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar3.Value;
            int SenIndex = (SensorIndex / 4) * 4;
            //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV3.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
            textBox_V3.Text = SensVal.ToString();
            SensiBuffer[SenIndex + 2] = SensVal;
        }

        private void trackBar4_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar4.Value;
            int SenIndex = (SensorIndex / 4) * 4;
            //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));labelV4.Text = Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
            textBox_V4.Text = SensVal.ToString();
            SensiBuffer[SenIndex + 3] = SensVal;
        }

        private void textBox_V1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBox_V1.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = (SensorIndex / 4) * 4;
                    //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));//Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                    textBox_V1.Text = SensVal.ToString();
                    trackBar1.Value = SensVal;
                    SensiBuffer[SenIndex + 0] = SensVal;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void textBox_V2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBox_V2.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = (SensorIndex / 4) * 4;
                    //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));//Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                    textBox_V2.Text = SensVal.ToString();
                    trackBar2.Value = SensVal;
                    SensiBuffer[SenIndex + 1] = SensVal;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox_V3_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBox_V3.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = (SensorIndex / 4) * 4;
                    //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));//Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                    textBox_V3.Text = SensVal.ToString();
                    trackBar3.Value = SensVal;
                    SensiBuffer[SenIndex + 2] = SensVal;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox_V4_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBox_V4.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = (SensorIndex / 4) * 4;
                    //double Percent = (double)(100.0 * ((double)SensVal / 4095.0));//Percent.ToString("F1") + "% \r\n(" + SensVal + ")";
                    textBox_V4.Text = SensVal.ToString();
                    trackBar4.Value = SensVal;
                    SensiBuffer[SenIndex + 3] = SensVal;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// =============================================================================================
        /// ====  Btn Sensor LED Click  ====
        /// =============================================================================================
        ///
        private void Btn_Col_R_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_R;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_R;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_R;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_R;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_G_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_G;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_G;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_G;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_G;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_B_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_B;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_B;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_B;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_B;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_Y_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_Y;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_Y;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_Y;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_Y;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_P_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_P;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_P;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_P;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_P;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_C_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_C;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_C;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_C;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_C;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_W_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_W;
            ColorBuffer[(SensorIndex / 4) * 4 + 1] = LED_COLOR_W;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_W;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_W;
            Sensor_Panel_Display_ReFlash(); 
        }


        /// =============================================================================================
        /// ====  Btn Sensor Debounce  Click  ====
        /// =============================================================================================
        ///
        private void trackBar_DTick_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar_DTick.Value;
            SetDebounceValue(SensVal); // FW V2.12: DTick[key]
            textBox_DTick.Text = SensVal.ToString();
        }

        private void textBox_DTick_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBox_DTick.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    if (SensVal < trackBar_DTick.Minimum) SensVal = trackBar_DTick.Minimum;
                    if (SensVal > trackBar_DTick.Maximum) SensVal = trackBar_DTick.Maximum;
                    textBox_DTick.Text = SensVal.ToString();
                    trackBar_DTick.Value = SensVal;
                    SetDebounceValue(SensVal); // FW V2.12: DTick[key]
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  Btn Default Click  ====
        /// =============================================================================================
        ///
        private void btn_default_Click(object sender, EventArgs e)
        {
            Load_Default_Sens();
            Sensor_Panel_Display_ReFlash();
        }
        /// =============================================================================================
        /// ====  Btn Read Click  ====
        /// =============================================================================================
        ///
        private void btn_Read_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            // 5Key FW: all keys in one WMCU packet (high nibble 0x00)
            // Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick 
            byte ConfigType = 0x00;
            //-------------------------------------------------------------------------------------------
            byte result = 0x00;
            if (tabControl1.SelectedIndex == 0)
            {
                ConfigType |= 0x00;
                result = Read_Config_PausingDebug(ConfigType, SensiBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 1)
            {
                ConfigType |= 0x01;
                result = Read_Config_PausingDebug(ConfigType, ColorBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 2)
            {
                ConfigType |= 0x02;
                result = Read_Config_PausingDebug(ConfigType, DTickBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            if (result != 0)
            {
                Sensor_Panel_Display_ReFlash();
            }
            //-------------------------------------------------------------------------------------------
        }
        /// =============================================================================================
        /// ====  Btn Write Click  ====
        /// =============================================================================================
        ///
        private void btn_Save_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            // 5Key FW: all keys in one WMCU packet (high nibble 0x00)
            // Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick 
            byte ConfigType = 0x00;
            //-------------------------------------------------------------------------------------------
            byte result = 0x00;
            if (tabControl1.SelectedIndex == 0)
            {
                ConfigType |= 0x00;
                result = Write_Config_PausingDebug(ConfigType, SensiBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 1)
            {
                ConfigType |= 0x01;
                result = Write_Config_PausingDebug(ConfigType, ColorBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 2)
            {
                ConfigType |= 0x02;
                result = Write_Config_PausingDebug(ConfigType, DTickBuffer, CONFIG_VALUE_COUNT);
            }
            //-------------------------------------------------------------------------------------------
            if (result != 0)
            {
                Sensor_Panel_Display_ReFlash();
            }
            //-------------------------------------------------------------------------------------------

        }

        /// =============================================================================================
        /// ====  Btn OTA  Click  ====
        /// =============================================================================================
        ///
        private void Btn_OTA_Click(object sender, EventArgs e)
        {
            // Open PADUEX5 upgrade tool (progress + anti-brick). It can enter OTA itself.
            App_OTA ota = new App_OTA();
            ota.ShowDialog(this);
            // Refresh device state after upgrade window closes
            Find_USB_Deivce();
        }

        /// =============================================================================================
        /// ====  Btn Debug  Click  ====
        /// =============================================================================================
        ///
        private void Btn_Debug_Click(object sender, EventArgs e)
        {
            SetDeviceRawDebugMode(!bDebugModesFG);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Seneor_Raw_Data_Read_and_Display();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            SendByteBuf[index++] = (byte)UserCommand.SET_MRAW;
            SendByteBuf[index++] = (byte)(0x43);    //C
            SendByteBuf[index++] = (byte)(0x4C);    //L
            SendByteBuf[index++] = (byte)(0x45);    //E
            SendByteBuf[index++] = (byte)(0x41);    //A
            SendByteBuf[index++] = (byte)(0x52);    //R
            //USBCtrl.DDR_USBDeviceCtrlOutput(mDeviceIndex, SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
        }


        /// =============================================================================================
        /// ====  Btn Read Mode  Click  ====
        /// =============================================================================================
        /////V1.0.0.16:20240921(1)Change DDR 1P and 2P Mode
        private void Btn_ReadMode_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            label_Ver.Text = "";
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)UserCommand.GET_MODE;
            //USBCtrl.DDR_USBDeviceCtrlOutput(mDeviceIndex, SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlInput(mDeviceIndex, InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_MODE)
            {
                if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MSEL_MAK) == (byte)UserMode.DDR_1PS)
                {
                    radioButton_1P.Checked = true;
                    radioButton_2P.Checked = false;
                }
                else
                {
                    radioButton_1P.Checked = false;
                    radioButton_2P.Checked = true;
                }

                if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.DDR_KBS)
                {
                    radioButton_KBS.Checked = true;
                    radioButton_HID.Checked = false;
                    radioButton_IO4.Checked = false;
                }
                else if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.DDR_HID)
                {
                    radioButton_KBS.Checked = false;
                    radioButton_HID.Checked = true;
                    radioButton_IO4.Checked = false;
                }
                else if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.DDR_IO4)
                {
                    radioButton_KBS.Checked = false;
                    radioButton_HID.Checked = false;
                    radioButton_IO4.Checked = true;
                }

                //V1.0.0.15:20240920(1)Change ADX TP Delay Config
                if (InputReportBuf[ReportIndex + 2] == (byte)UserProID.ID_DDR)
                {
                    if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MSEL_MAK) == (byte)UserMode.DDR_1PS)
                    {
                        label_Ver.Text = "1P : ";
                    }
                    else
                    {
                        label_Ver.Text = "2P : ";
                    }
                    label_Ver.Text += "UID=" + InputReportBuf[ReportIndex + 2].ToString("X02") + ", " +
                                     "HVer=" + InputReportBuf[ReportIndex + 3].ToString("X02") + ", " +
                                     "FVer=" + InputReportBuf[ReportIndex + 4].ToString() + "." + InputReportBuf[ReportIndex + 5].ToString("D02");
                }
                else
                {
                    label_Ver.Text = "";
                }
            }
            //-------------------------------------------------------------------------------------------
            string RxStr = "GetInputReport:";
            for (int i = 0; i < ReportSize - ReportIndex; i++)
            {
                RxStr += InputReportBuf[ReportIndex + i].ToString("X02");
            }
            Console.WriteLine(RxStr);
            //Console.Write("GetInputReport:" + ex.Message);
            //-------------------------------------------------------------------------------------------
        }

        private void Btn_SaveMode_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte index = 0;
            //byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //byte Txd_CS = 0;
            //-------------------------------------------------------------------------------------------
            byte WorkMode = 0;
            if (radioButton_1P.Checked == true)
            {
                WorkMode = (byte)UserMode.DDR_1PS;
            }
            else
            {
                WorkMode = (byte)UserMode.DDR_2PS;
            }
            if(radioButton_KBS.Checked == true)
            {
                WorkMode |= (byte)UserMode.DDR_KBS;
            }
            else if (radioButton_HID.Checked == true)
            {
                WorkMode |= (byte)UserMode.DDR_HID;
            }
            else if (radioButton_IO4.Checked == true)
            {
                WorkMode |= (byte)UserMode.DDR_IO4;
            }
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_MODE);
            SendByteBuf[index++] = (byte)(WorkMode);
            //-------------------------------------------------------------------------------------------
            //USBCtrl.DDR_USBDeviceCtrlOutput(mDeviceIndex, SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            Btn_ReadMode_Click(sender, e);
            //-------------------------------------------------------------------------------------------
        }

        private void checkBox_1P_CheckedChanged(object sender, EventArgs e)
        {
            //checkBox_2P.Checked = false;
        }

        private void checkBox_2P_CheckedChanged(object sender, EventArgs e)
        {
            //checkBox_1P.Checked = false;
        }

        private void radioButton_1P_CheckedChanged(object sender, EventArgs e)
        {
            mDeviceIndex = 0;
        }

        private void radioButton_2P_CheckedChanged(object sender, EventArgs e)
        {
            mDeviceIndex = 1;
        }


        

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
    }
}
