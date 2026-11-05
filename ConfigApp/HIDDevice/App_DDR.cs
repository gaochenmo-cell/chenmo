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
        private bool bDarkMode = false;
        private bool bDebugModesFG = false;
        private readonly bool[] padKeyActive = new bool[5];
        private byte mDeviceIndex = 0;
        // 1P/2P is fixed by firmware since V2.12 (DEFAULT_1P2P); games assign players by
        // plug-in order, so the host no longer exposes it. Echo back whatever the device reports.
        private byte mDevicePlayerBits = (byte)UserMode.DDR_2PS;

        // Device identity from the last *successful* GET_MODE. mFwValid is false until a reply
        // has been parsed and is cleared again on disconnect / OTA / failed read, so a previous
        // board's version can never be shown or used for the key-map capability check.
        private bool mFwValid = false;
        private int mDevUid = 0, mDevHVer = 0;
        private int mFwMajor = 0, mFwMinor = 0;
        // Why the last GET_MODE failed (resource key, "" = unknown). Shown in the label so the
        // user sees "no reply" / "board in RAW debug mode" / "HID read failed" instead of one
        // generic "not read".
        private string mFwFailReasonKey = "";
        // GET_MODE: first read 100 ms after the request (same as 1.0.0.57), then re-send and
        // re-read a few times. A board left in RAW debug mode by a previous session overwrites
        // every reply with "AMRAW" frames; when we see one we send NOLMD and try again.
        private const int GET_MODE_RETRIES = 6;
        private const int GET_MODE_WAIT_MS = 100;
        private const int GET_SENS_RETRIES = 3;

        /// <summary>Firmware bytes are plain integers: major=2, minor=12 → "2.12".</summary>
        private string FwVersionText()
        {
            return mFwMajor.ToString() + "." + mFwMinor.ToString("D02");
        }

        // Only tag versions we actually know. Stock V2.12, stock V2.13 (its FW_VERSION_MINOR
        // constant was never bumped) and paduex3_1.6 all report 2.12; paduex3_1.7 is the only image
        // that reports 2.17. Anything else is shown as-is without a guess.
        // UID / HVer that differ from the known board are annotated, never rejected.
        private string FwTag()
        {
            if (!mFwValid) return "";
            string tag = "";
            if (mFwMajor == 2 && mFwMinor == 17) tag = L.Get("Ver.Tag.Paduex3_17");
            else if (mFwMajor == 2 && mFwMinor == 18) tag = L.Get("Ver.Tag.Paduex3_18_4key");
            else if (mFwMajor == 2 && mFwMinor == 12) tag = L.Get("Ver.Tag.Stock212");
            if (mDevUid != DeviceProtocol.ID_DDR) tag += L.Get("Ver.Tag.UidMismatch");
            if (mDevHVer != DeviceProtocol.HVER_KNOWN) tag += L.Get("Ver.Tag.HVerUnknown");
            return tag;
        }
        private bool FwHasKeyMap() { return mFwValid && mFwMajor == 2 && mFwMinor >= 17; }

        private void ResetDeviceVersion()
        {
            mFwValid = false;
            mDevUid = mDevHVer = 0;
            mFwMajor = mFwMinor = 0;
            mFwFailReasonKey = "";
        }

        private void UpdateVersionLabel()
        {
            if (label_Ver == null) return;
            if (!bDeviceStatusFG)
            {
                label_Ver.Text = "";
                return;
            }
            if (!mFwValid)
            {
                label_Ver.Text = mFwFailReasonKey.Length > 0
                    ? L.F("Ver.NotReadReason", L.Get(mFwFailReasonKey))
                    : L.Get("Ver.NotRead");
                return;
            }
            label_Ver.Text = L.F("Ver.Label",
                mDevUid.ToString("X02"),
                mDevHVer.ToString("X02"),
                FwVersionText(),
                FwTag());
        }

        private byte DefaultOffset = 0x01;			//V1.0.0.18:20241012(1)Change ADX Save Issue
        ///-------------------------------------------------------------------------------------------
        // Config.ini KEYCOUNT=4|5 (default 5). Same code, same protocol; only the pad grid,
        // the key-map rows and the window title change. Read once at start-up (ReadAppConfig).
        private static int KeyCount = KEY_COUNT_5KEY;
        private static bool FourKeyMode { get { return KeyCount == 4; } }
        private static string AppName { get { return FourKeyMode ? "PADUEX4" : "PADUEX5"; } }
        private static readonly string HostVer = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private enum ConnState { None, Device, Ota }
        private ConnState mConnState = ConnState.None;

        private void UpdateWindowTitle()
        {
            string suffix = "";
            if (mConnState == ConnState.Device) suffix = " " + L.Get("Title.Connected");
            else if (mConnState == ConnState.Ota) suffix = " " + L.Get("Title.OtaConnected");
            this.Text = AppName + " " + HostVer + suffix;
        }

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
     
             public Color TrackColor = Color.FromArgb(229, 231, 235);
             public Color FillColor = Color.FromArgb(47, 111, 237);
             public Color TextColor = Color.FromArgb(31, 41, 55);
             public Color FrameColor = Color.FromArgb(209, 213, 219);

             protected override void OnPaintBackground(PaintEventArgs e)
             {
                 Color behind = Parent != null ? Parent.BackColor : BackColor;
                 e.Graphics.Clear(behind);
             }

             protected override void OnPaint(PaintEventArgs e)
             {
                 Rectangle rect = ClientRectangle;
                 Graphics g = e.Graphics;
                 g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                 Rectangle outer = new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                 int radius = Math.Max(3, Math.Min(outer.Width, outer.Height) / 4);
                 using (System.Drawing.Drawing2D.GraphicsPath track = PadKeyPainter.RoundedRect(outer, radius))
                 {
                     using (SolidBrush b = new SolidBrush(TrackColor))
                         g.FillPath(b, track);
                     using (Pen p = new Pen(FrameColor, 1f))
                         g.DrawPath(p, track);
                 }
                 if (Value > 0 && Maximum > 0)
                 {
                     Rectangle fill;
                     if (direction == ProgressBarDirection.Horizontal)
                     {
                         int w = (int)Math.Round(((float)Value / Maximum) * outer.Width);
                         fill = new Rectangle(outer.X, outer.Y, Math.Max(w, radius * 2 + 1), outer.Height);
                     }
                     else
                     {
                         int h = (int)Math.Round(((float)Value / Maximum) * outer.Height);
                         h = Math.Max(h, radius * 2 + 1);
                         fill = new Rectangle(outer.X, outer.Bottom - h, outer.Width, h);
                     }
                     fill.Intersect(outer);
                     using (System.Drawing.Drawing2D.GraphicsPath fp = PadKeyPainter.RoundedRect(fill, radius))
                     using (SolidBrush b = new SolidBrush(FillColor))
                         g.FillPath(b, fp);
                 }

                 string text = Value.ToString();
                 using (Font f = UiFont.Get(UiFont.BarValue, FontStyle.Bold))
                 using (SolidBrush tb = new SolidBrush(TextColor))
                 {
                     SizeF len = g.MeasureString(text, f);
                     PointF location = new PointF(rect.Width / 2f - len.Width / 2f, rect.Height / 2f - len.Height / 2f);
                     g.DrawString(text, f, tb, location);
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
            L.LanguageChanged += (s, e) => { if (!IsDisposed) OnLanguageChanged(); };
            DebugLog.Log("==== PADUEX5 {0} start, config={1}, lang={2}, log={3} ====",
                HostVer, AppConfig.Path, L.Setting, DebugLog.Enabled ? "on" : "off");
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

        /// =============================================================================================
        /// ==== 4-key (KEYCOUNT=4) layout: same board, only connectors K1..K4 populated ====
        ///   [    ] [2 ↑ ] [    ]
        ///   [1 ← ] [    ] [4 → ]
        ///   [    ] [3 ↓ ] [    ]
        /// Firmware key index 0..3 (Btn_K1..K4) → arrow. Key index 4 (K5) is never shown; its 4
        /// config slots are still transferred unchanged so the 20-value packets stay intact.
        /// Mapping assumed K1=←, K2=↑, K3=↓, K4=→ — to be confirmed on a real 4-key pad.
        /// =============================================================================================
        private static readonly PadKeyPainter.PadKeyKind[] FourKeyKinds = new PadKeyPainter.PadKeyKind[] {
            PadKeyPainter.PadKeyKind.Left, PadKeyPainter.PadKeyKind.Up,
            PadKeyPainter.PadKeyKind.Down, PadKeyPainter.PadKeyKind.Right
        };
        private static readonly string[] MapPadKeys4 = new string[] { "Map.Pad.L", "Map.Pad.U", "Map.Pad.D", "Map.Pad.R" };

        /// <summary>Firmware key index (0..4) of a pad glyph, in either layout; -1 if not shown.</summary>
        private static int KeyIndexOfKind(PadKeyPainter.PadKeyKind kind)
        {
            if (FourKeyMode)
                return Array.IndexOf(FourKeyKinds, kind);
            int k = (int)kind;
            return k < KEY_COUNT_5KEY ? k : -1;
        }

        /// <summary>Is firmware key index k one of the keys this build shows / edits?</summary>
        private static bool IsVisibleKey(int k) { return k >= 0 && k < KeyCount; }

        private string PadKeyCaptionKey(int k)
        {
            return FourKeyMode && k < MapPadKeys4.Length ? MapPadKeys4[k] : MapPadKeys[k];
        }
        // Design canvas at 96 DPI — all layout math uses S(designPx)
        private const int DesignW = 1040;
        private const int DesignH = 780;
        private const int TabStripH = 36;   // tab header height (design px) — fits the 11pt tab caption
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
        private Label[] mapKeyLabels;
        private static readonly string[] MapPadKeys = new string[] { "Map.Pad.UL", "Map.Pad.UR", "Map.Pad.C", "Map.Pad.DL", "Map.Pad.DR" };
        private Button btn_MapRead;
        private Button btn_MapSave;
        private readonly int[] KeyMappingBuffer = new int[KEY_COUNT_5KEY];
        // Stock Report_Normal tables. 1P: W X A D S   2P: H K J N M (firmware fixes 2P since V2.12)
        private readonly int[] DefaultKeyMapping1P = new int[] { 0x1A, 0x1B, 0x04, 0x07, 0x16 };
        private readonly int[] DefaultKeyMapping2P = new int[] { 0x0B, 0x0E, 0x0D, 0x11, 0x10 };
        // paduex3_1.7 keymap lives in the unused Color[key*4+1] slot, tagged 0x4B00 ("K") | usage.
        private const int MAP_TAG = 0x4B;
        private Button btn_MapDefault;

        private int[] DefaultKeyMapping
        {
            get { return mDevicePlayerBits == (byte)UserMode.DDR_1PS ? DefaultKeyMapping1P : DefaultKeyMapping2P; }
        }

        private static bool IsMapSlot(int i) { return (i % 4) == 1; }

        private void DecodeKeyMapFromColor()
        {
            for (int k = 0; k < KEY_COUNT_5KEY; k++)
            {
                int v = ColorBuffer[k * 4 + 1];
                KeyMappingBuffer[k] = ((v >> 8) & 0xFF) == MAP_TAG ? (v & 0x7F) : DefaultKeyMapping[k];
            }
        }

        private void EncodeKeyMapIntoColor(bool clear)
        {
            // Keys this build does not show (K5 in 4-key mode) keep whatever slot value was read
            for (int k = 0; k < KeyCount; k++)
                ColorBuffer[k * 4 + 1] = clear ? 0 : ((MAP_TAG << 8) | (KeyMappingBuffer[k] & 0x7F));
        }

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
            UpdateWindowTitle();
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
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
                KeyMappingBuffer[i] = DefaultKeyMapping[i];
            EnsureKeyMappingUi();
            EnsurePadKeyVectorPaint();
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

            Point[] fillerPos;
            if (FourKeyMode)
            {
                // Cross: K1 ←, K2 ↑, K3 ↓, K4 → ; K5 unused (see FourKeyKinds)
                Btn_K1.Location = new Point(x0, y1);
                Btn_K2.Location = new Point(x1, y0);
                Btn_K3.Location = new Point(x1, y2);
                Btn_K4.Location = new Point(x2, y1);
                Btn_K5.Location = new Point(x2, y2);
                Btn_K5.Enabled = false;
                Btn_K5.Visible = false;
                fillerPos = new Point[] {
                    new Point(x0, y0), new Point(x2, y0), new Point(x1, y1), new Point(x0, y2), new Point(x2, y2)
                };
            }
            else
            {
                Btn_K1.Location = new Point(x0, y0);
                Btn_K2.Location = new Point(x2, y0);
                Btn_K3.Location = new Point(x1, y1);
                Btn_K4.Location = new Point(x0, y2);
                Btn_K5.Location = new Point(x2, y2);
                Btn_K5.Enabled = true;
                Btn_K5.Visible = true;
                fillerPos = new Point[] {
                    new Point(x1, y0), new Point(x0, y1), new Point(x2, y1), new Point(x1, y2)
                };
            }
            Btn_K1.Size = Btn_K2.Size = Btn_K3.Size = Btn_K4.Size = Btn_K5.Size = cellSize;
            Btn_K1.Enabled = Btn_K2.Enabled = Btn_K3.Enabled = Btn_K4.Enabled = true;
            Btn_K1.Visible = Btn_K2.Visible = Btn_K3.Visible = Btn_K4.Visible = true;
            InvalidatePadKeyButtons();

            if (fiveKeyFillers == null)
            {
                fiveKeyFillers = new Panel[fillerPos.Length];
                for (int i = 0; i < fillerPos.Length; i++)
                {
                    Panel p = new Panel();
                    p.BackColor = Color.FromArgb(245, 245, 245);
                    p.BorderStyle = BorderStyle.None;
                    p.Enabled = false;
                    p.Paint += FillerPanel_Paint;
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
            if (tabControl1.DrawMode == TabDrawMode.OwnerDrawFixed && tabControl1.TabPages.Count > 0)
                tabControl1.ItemSize = new Size(Math.Max(S(80), (tabW - S(6)) / tabControl1.TabPages.Count), S(TabStripH));

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
            label_Ver.AutoSize = true;
            label_Ver.Location = new Point(ox + S(2), S(12));
            label_Ver.BringToFront();
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

        private ContextMenuStrip menuLanguage;
        private ToolStripMenuItem menuLangAuto, menuLangZh, menuLangEn;

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

        private void RefreshLanguageMenu()
        {
            if (menuLanguage == null) return;
            menuLangAuto.Text = L.Get("Lang.Auto");
            menuLangZh.Text = L.Get("Lang.zhCN");
            menuLangEn.Text = L.Get("Lang.en");
            menuLangAuto.Checked = L.Setting == L.SettingAuto;
            menuLangZh.Checked = L.Setting == L.SettingChinese;
            menuLangEn.Checked = L.Setting == L.SettingEnglish;
            // Match the active theme so the menu is readable in dark mode too
            menuLanguage.Renderer = new ToolStripProfessionalRenderer(new MenuColors(pal));
            menuLanguage.BackColor = pal.PanelBg;
            menuLanguage.ForeColor = pal.Text;
            foreach (ToolStripItem it in menuLanguage.Items)
            {
                it.BackColor = pal.PanelBg;
                it.ForeColor = pal.Text;
            }
        }

        private void SetLanguage(string setting)
        {
            L.Apply(setting);                 // raises LanguageChanged → OnLanguageChanged()
            AppConfig.Write("LANGUAGE", L.Setting);
        }

        /// <summary>Re-apply every localized caption and repaint owner-drawn chrome (tabs, group titles).</summary>
        private void OnLanguageChanged()
        {
            ApplyUiLanguage();
            if (layoutReady && !layoutBusy)
                RebuildScaledLayout();
            this.Invalidate(true);
        }

        private sealed class MenuColors : ProfessionalColorTable
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
            label_MapHint.Size = new Size(S(440), S(62));   // three lines of body text (English hint wraps to 3)
            groupBox_Map.Controls.Add(label_MapHint);

            label_MapKey = new Label();
            label_MapKey.AutoSize = false;
            label_MapKey.Location = new Point(S(16), S(96));
            label_MapKey.Size = new Size(S(440), S(24));
            groupBox_Map.Controls.Add(label_MapKey);

            mapKeyCombos = new ComboBox[KEY_COUNT_5KEY];
            mapKeyLabels = new Label[KEY_COUNT_5KEY];
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
            {
                Label lb = new Label();
                lb.Text = L.Get(PadKeyCaptionKey(i));
                lb.Location = new Point(S(16), S(128) + i * S(42));
                lb.Size = new Size(S(100), S(28));
                lb.Visible = IsVisibleKey(i);
                groupBox_Map.Controls.Add(lb);
                mapKeyLabels[i] = lb;

                ComboBox cb = new ComboBox();
                cb.DropDownStyle = ComboBoxStyle.DropDownList;
                cb.Location = new Point(S(130), S(126) + i * S(42));
                cb.Size = new Size(S(200), S(28));
                cb.Visible = IsVisibleKey(i);
                cb.Enabled = IsVisibleKey(i);
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

            btn_MapDefault = new Button();
            btn_MapDefault.Name = "btn_MapDefault";
            btn_MapDefault.Location = new Point(S(324), S(350));
            btn_MapDefault.Size = new Size(S(140), S(36));
            btn_MapDefault.Click += (s, e) => Reset_KeyMapping_Config();
            groupBox_Map.Controls.Add(btn_MapDefault);

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

        // Common NKRO usages (HID keyboard page). Names starting with '@' are resource keys.
        private static readonly string[][] HidUsageTable = new string[][] {
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
            new[]{"2C","@Hid.Space"}, new[]{"28","@Hid.Enter"}, new[]{"2A","@Hid.Backspace"},
            new[]{"50","@Hid.Left"}, new[]{"4F","@Hid.Right"}, new[]{"52","@Hid.Up"}, new[]{"51","@Hid.Down"},
            new[]{"3A","F1"}, new[]{"3B","F2"}, new[]{"3C","F3"},
            new[]{"59","@Hid.Numpad|1"}, new[]{"5A","@Hid.Numpad|2"}, new[]{"5B","@Hid.Numpad|3"}, new[]{"5C","@Hid.Numpad|4"},
            new[]{"5D","@Hid.Numpad|5"}, new[]{"5E","@Hid.Numpad|6"}, new[]{"5F","@Hid.Numpad|7"}, new[]{"60","@Hid.Numpad|8"},
            new[]{"61","@Hid.Numpad|9"}, new[]{"62","@Hid.Numpad|0"},
        };

        private static string HidUsageDisplayName(string[] row)
        {
            string name = row[1];
            if (name.StartsWith("@"))
            {
                int bar = name.IndexOf('|');
                name = bar > 0 ? L.F(name.Substring(1, bar - 1), name.Substring(bar + 1)) : L.Get(name.Substring(1));
            }
            return name + " (0x" + row[0] + ")";
        }

        private void FillHidUsageCombo(ComboBox cb)
        {
            cb.Items.Clear();
            foreach (string[] row in HidUsageTable)
            {
                int u = Convert.ToInt32(row[0], 16);
                cb.Items.Add(new HidUsageItem { Usage = u, Name = HidUsageDisplayName(row) });
            }
        }

        /// <summary>Language switch: rebuild the combo item captions without losing the selection.</summary>
        private void RelocalizeHidUsageCombos()
        {
            if (mapKeyCombos == null) return;
            foreach (ComboBox cb in mapKeyCombos)
            {
                int keep = cb.SelectedItem is HidUsageItem sel ? sel.Usage : -1;
                cb.BeginUpdate();
                FillHidUsageCombo(cb);
                cb.EndUpdate();
                if (keep >= 0)
                {
                    for (int j = 0; j < cb.Items.Count; j++)
                    {
                        HidUsageItem it = cb.Items[j] as HidUsageItem;
                        if (it != null && it.Usage == keep) { cb.SelectedIndex = j; break; }
                    }
                }
            }
            SyncMapCombosFromBuffer();
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
            bool kbs = radioButton_KBS.Checked;
            groupBox2.Visible = false;
            if (tabPage_Map != null)
                tabPage_Map.Enabled = kbs;
        }

        private void Read_KeyMapping_Config()
        {
            if (!bDeviceStatusFG)
            {
                MessageBox.Show(L.Get("Map.NoDevice"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Colour table (type 1) carries the keymap in slot key*4+1 — no new USB command needed.
            if (Read_Config_PausingDebug(0x01, ColorBuffer, CONFIG_VALUE_COUNT) == 0)
                return;
            DecodeKeyMapFromColor();
            SyncMapCombosFromBuffer();
            Sensor_Panel_Display_ReFlash();
            bool tagged = false;
            for (int k = 0; k < KeyCount; k++)
                tagged |= ((ColorBuffer[k * 4 + 1] >> 8) & 0xFF) == MAP_TAG;
            MessageBox.Show(L.Get(tagged ? "Map.ReadOk" : "Map.ReadDefault"),
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Save_KeyMapping_Config()
        {
            WriteKeyMap(false);
        }

        private void Reset_KeyMapping_Config()
        {
            for (int k = 0; k < KEY_COUNT_5KEY; k++)
                KeyMappingBuffer[k] = DefaultKeyMapping[k];
            SyncMapCombosFromBuffer();
            if (bDeviceStatusFG)
                WriteKeyMap(true);
        }

        private void WriteKeyMap(bool clear)
        {
            if (!bDeviceStatusFG)
            {
                MessageBox.Show(L.Get("Map.NoDevice"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!clear && !FwHasKeyMap())
            {
                // Version may be stale/unknown: re-read it right now instead of trusting a cached value
                if (!mFwValid) ReadDeviceMode();
                if (!FwHasKeyMap())
                {
                    string ver = mFwValid ? FwVersionText() : "?";
                    DialogResult r = MessageBox.Show(L.F("Map.FwNoSupport", ver),
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes)
                        return;
                }
            }
            // Refresh LED colours first so we never clobber them with stale data
            if (Read_Config_PausingDebug(0x01, ColorBuffer, CONFIG_VALUE_COUNT) == 0)
                return;
            EncodeKeyMapIntoColor(clear);
            if (Write_Config_PausingDebug(0x01, ColorBuffer, CONFIG_VALUE_COUNT) == 0)
                return;
            MessageBox.Show(L.Get(clear ? "Map.ResetOk" : "Map.SaveOk"),
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            // Caption names the theme you will switch TO
            btnThemeToggle.Text = L.Get(bDarkMode ? "Btn.ThemeLight" : "Btn.ThemeDark");
        }

        private void WriteThemeToConfig()
        {
            AppConfig.Write("THEME", bDarkMode ? "DARK" : "LIGHT");
        }

        /// <summary>
        /// Light: Laverita palette (Tailwind gray scale + blue / amber / red accents).
        /// Dark: neon purple (deep violet-black background, violet→pink gradient highlights with glow).
        /// Gradient / glow fields are Color.Empty on the light palette, which disables those effects.
        /// </summary>
        private sealed class Palette
        {
            public Color FormBg, PanelBg, CardBg, Border, BorderStrong, Text, MutedText, Title;
            public Color Accent, AccentText, Danger, Neutral, NeutralText, Selected, BarTrack, BarFill;
            public Color PadOutline;          // arrow / ring stroke on an unselected pad key
            public Color SelectedGradientEnd; // second stop of the selected pad key fill (Empty = solid)
            public Color AccentGradientEnd;   // second stop of the tab underline (Empty = solid)
            public Color Glow;                // outer glow around selected / pressed pad keys (Empty = none)
            public Color ArrowTip, ArrowMid, ArrowBack, ArrowGlow; // chevron / ring cyan gradient + halo

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
                return p;
            }
        }

        private Palette pal = Palette.Light();
        private bool themeHooksWired = false;

        private void ApplyTheme()
        {
            pal = bDarkMode ? Palette.Dark() : Palette.Light();
            WireThemeHooks();

            // Typography is applied in the same recursive pass as colours (see ThemeControl):
            // the form carries the body font so any control without its own size inherits it.
            UiFont.Set(this, UiFont.Body);
            this.BackColor = pal.FormBg;
            this.ForeColor = pal.Text;
            ThemeChildren(this);

            // Form-level widgets sit directly on the page background
            if (fiveKeyFillers != null)
            {
                foreach (Panel p in fiveKeyFillers)
                {
                    p.BackColor = pal.CardBg;
                    p.BorderStyle = BorderStyle.None;
                }
            }
            StyleButton(btnLangToggle, pal.Neutral, pal.NeutralText);
            StyleButton(btnThemeToggle, pal.Accent, pal.AccentText);
            if (label_Ver != null)
            {
                label_Ver.BackColor = pal.FormBg;
                label_Ver.ForeColor = pal.MutedText;
                UiFont.Set(label_Ver, UiFont.Status);
            }
            if (menuLanguage != null)
                UiFont.Set(menuLanguage, UiFont.Body);

            ApplyModeGroupColors();
            UpdateThemeToggleText();
            InvalidatePadKeyButtons();
            this.Invalidate(true);
        }

        private bool modeGroupEnabled = true;

        /// <summary>Disabling the GroupBox itself paints every label with the system gray-text colour,
        /// which is unreadable on the dark palette; disable the children instead.</summary>
        private void SetModeGroupEnabled(bool enabled)
        {
            modeGroupEnabled = enabled;
            Btn_ReadMode.Enabled = Btn_SaveMode.Enabled = enabled;
            if (btnFirmwareUpgrade != null) btnFirmwareUpgrade.Enabled = enabled;
            foreach (RadioButton rb in new RadioButton[] { radioButton_KBS, radioButton_HID, radioButton_IO4, radioButton_1P, radioButton_2P })
            {
                rb.AutoCheck = enabled;
                rb.Cursor = enabled ? Cursors.Default : Cursors.No;
            }
            ApplyModeGroupColors();
        }

        private void ApplyModeGroupColors()
        {
            Color fore = modeGroupEnabled ? pal.Text : pal.MutedText;
            foreach (RadioButton rb in new RadioButton[] { radioButton_KBS, radioButton_HID, radioButton_IO4, radioButton_1P, radioButton_2P })
                rb.ForeColor = fore;
            foreach (Button b in new Button[] { Btn_ReadMode, Btn_SaveMode, btnFirmwareUpgrade })
            {
                if (b == null) continue;
                b.BackColor = modeGroupEnabled ? pal.Accent : pal.Neutral;
                b.ForeColor = modeGroupEnabled ? pal.AccentText : pal.MutedText;
            }
        }

        private void WireThemeHooks()
        {
            if (themeHooksWired)
                return;
            themeHooksWired = true;
            tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.ItemSize = new Size(Math.Max(S(80), (tabControl1.Width - S(6)) / Math.Max(1, tabControl1.TabPages.Count)), S(TabStripH));
            tabControl1.Padding = new Point(12, 4);
            tabControl1.DrawItem += TabControl_DrawItem;
        }

        private void ThemeChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                ThemeControl(c);
                if (c.HasChildren)
                    ThemeChildren(c);
            }
        }

        private void ThemeControl(Control c)
        {
            if (c is GroupBox gb)
            {
                gb.BackColor = pal.PanelBg;
                gb.ForeColor = pal.Title;
                UiFont.Set(gb, UiFont.Body);   // children inherit body; the bold title is drawn in GroupBox_Paint
                gb.Paint -= GroupBox_Paint;
                gb.Paint += GroupBox_Paint;
                return;
            }
            if (c is TabControl tc)
            {
                tc.BackColor = pal.FormBg;
                tc.ForeColor = pal.Text;
                UiFont.Set(tc, UiFont.Tab);
                return;
            }
            if (c is TabPage tp)
            {
                tp.UseVisualStyleBackColor = false;
                tp.BackColor = pal.PanelBg;
                tp.ForeColor = pal.Text;
                tp.BorderStyle = BorderStyle.None;
                UiFont.Set(tp, UiFont.Body);
                return;
            }
            if (c is Button b)
            {
                ThemeButton(b);
                return;
            }
            if (c is ProgressBarWithValue pb)
            {
                pb.TrackColor = pal.BarTrack;
                pb.FillColor = pal.Accent;
                pb.FrameColor = pal.Border;
                pb.TextColor = pal.Text;
                pb.BackColor = pal.PanelBg;
                pb.Invalidate();
                return;
            }
            if (c is TrackBar tb)
            {
                tb.BackColor = pal.PanelBg;
                return;
            }
            if (c is TextBox tx)
            {
                tx.BorderStyle = BorderStyle.FixedSingle;
                tx.BackColor = pal.CardBg;
                tx.ForeColor = pal.Text;
                UiFont.Set(tx, IsValueControl(tx) ? UiFont.Value : UiFont.Body);
                return;
            }
            if (c is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = pal.CardBg;
                cb.ForeColor = pal.Text;
                UiFont.Set(cb, UiFont.Body);
                return;
            }
            if (c is RadioButton || c is CheckBox)
            {
                c.BackColor = pal.PanelBg;
                c.ForeColor = pal.Text;
                UiFont.Set(c, UiFont.Body);
                return;
            }
            if (c is Label lb)
            {
                lb.BackColor = Color.Transparent;
                lb.ForeColor = (lb == label_MapHint) ? pal.MutedText : pal.Text;
                if (lb == label_Ver)
                    UiFont.Set(lb, UiFont.Status);
                else
                    UiFont.Set(lb, IsValueControl(lb) ? UiFont.Value : UiFont.Body);
                return;
            }
            if (c is PictureBox)
            {
                c.BackColor = pal.PanelBg;
                return;
            }
            if (c is Panel)
            {
                c.BackColor = pal.CardBg;
                return;
            }
        }

        /// <summary>Sensitivity / debounce numbers and their A–D column captions use the larger value font.</summary>
        private bool IsValueControl(Control c)
        {
            return c == textBox_V1 || c == textBox_V2 || c == textBox_V3 || c == textBox_V4 || c == textBox_DTick
                || c == label1 || c == label2 || c == label3 || c == label4 || c == label_DTick;
        }

        private void ThemeButton(Button b)
        {
            // Pad keys and LED colour swatches keep their own paint / colour
            if (b.Tag is PadKeyPainter.PadKeyKind)
                return;
            if (b == Btn_Col_R || b == Btn_Col_G || b == Btn_Col_B || b == Btn_Col_Y ||
                b == Btn_Col_P || b == Btn_Col_C || b == Btn_Col_W || b == Btn_LED)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = pal.BorderStrong;
                return;
            }
            if (b == btnLangToggle)
            {
                StyleButton(b, pal.Neutral, pal.NeutralText);
                return;
            }
            // "Default / reset" actions are red like Laverita; everything else is the blue accent
            if (b == btn_default || b == button2 || (b.Name != null && b.Name.IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                StyleButton(b, pal.Danger, Color.White);
                return;
            }
            StyleButton(b, pal.Accent, pal.AccentText);
        }

        private void StyleButton(Button b, Color back, Color fore)
        {
            if (b == null)
                return;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Lighten(back, bDarkMode ? 0.12f : -0.08f);
            b.FlatAppearance.MouseDownBackColor = Lighten(back, bDarkMode ? 0.2f : -0.16f);
            b.UseVisualStyleBackColor = false;
            b.BackColor = back;
            b.ForeColor = fore;
            UiFont.Set(b, UiFont.Body, FontStyle.Bold);
        }

        private static Color Lighten(Color c, float amount)
        {
            // amount > 0 lightens, < 0 darkens
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

        /// <summary>Card look: rounded panel, 1px border, blue bold title (no system GroupBox frame).</summary>
        private void GroupBox_Paint(object sender, PaintEventArgs e)
        {
            GroupBox gb = sender as GroupBox;
            if (gb == null)
                return;
            Graphics g = e.Graphics;
            Color behind = gb.Parent != null ? gb.Parent.BackColor : pal.FormBg;
            g.Clear(behind);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, gb.Width - 1, gb.Height - 1);
            using (System.Drawing.Drawing2D.GraphicsPath path = PadKeyPainter.RoundedRect(r, S(8)))
            {
                using (SolidBrush fill = new SolidBrush(pal.PanelBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(pal.Border, 1f))
                    g.DrawPath(pen, path);
            }
            if (!string.IsNullOrEmpty(gb.Text))
            {
                using (Font f = UiFont.Get(UiFont.GroupTitle, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(pal.Title))
                    g.DrawString(gb.Text, f, tb, S(10), S(4));
            }
        }

        private void FillerPanel_Paint(object sender, PaintEventArgs e)
        {
            Panel p = sender as Panel;
            if (p == null)
                return;
            e.Graphics.Clear(pal.FormBg);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, 1, p.Width - 3, p.Height - 3);
            using (System.Drawing.Drawing2D.GraphicsPath path = PadKeyPainter.RoundedRect(r, Math.Max(4, p.Width / 14)))
            {
                using (SolidBrush fill = new SolidBrush(pal.CardBg))
                    e.Graphics.FillPath(fill, path);
                using (Pen pen = new Pen(pal.Border, 1f))
                    e.Graphics.DrawPath(pen, path);
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (tc == null || e.Index < 0 || e.Index >= tc.TabPages.Count)
                return;
            bool selected = e.Index == tc.SelectedIndex;
            Rectangle r = e.Bounds;
            using (SolidBrush bg = new SolidBrush(selected ? pal.PanelBg : pal.FormBg))
                e.Graphics.FillRectangle(bg, r);
            if (selected)
            {
                Rectangle ul = new Rectangle(r.X + 2, r.Bottom - 3, r.Width - 4, 3);
                if (pal.AccentGradientEnd.IsEmpty)
                {
                    using (SolidBrush underline = new SolidBrush(pal.Accent))
                        e.Graphics.FillRectangle(underline, ul);
                }
                else
                {
                    using (System.Drawing.Drawing2D.LinearGradientBrush underline =
                        new System.Drawing.Drawing2D.LinearGradientBrush(ul, pal.Accent, pal.AccentGradientEnd, 0f))
                        e.Graphics.FillRectangle(underline, ul);
                }
            }
            using (Font f = UiFont.Get(UiFont.Tab, selected ? FontStyle.Bold : FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(selected ? pal.Title : pal.MutedText))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(tc.TabPages[e.Index].Text, f, tb, r, sf);
            }
        }

        private void EnsurePadKeyVectorPaint()
        {
            if (padKeyVectorWired)
                return;
            if (FourKeyMode)
            {
                PadKeyPainter.WireButton(Btn_K1, FourKeyKinds[0]);
                PadKeyPainter.WireButton(Btn_K2, FourKeyKinds[1]);
                PadKeyPainter.WireButton(Btn_K3, FourKeyKinds[2]);
                PadKeyPainter.WireButton(Btn_K4, FourKeyKinds[3]);
            }
            else
            {
                PadKeyPainter.WireButton(Btn_K1, PadKeyPainter.PadKeyKind.UpperLeft);
                PadKeyPainter.WireButton(Btn_K2, PadKeyPainter.PadKeyKind.UpperRight);
                PadKeyPainter.WireButton(Btn_K3, PadKeyPainter.PadKeyKind.Center);
                PadKeyPainter.WireButton(Btn_K4, PadKeyPainter.PadKeyKind.DownLeft);
            }
            PadKeyPainter.WireButton(Btn_K5, PadKeyPainter.PadKeyKind.DownRight);
            padKeyVectorWired = true;
        }

        internal PadKeyPainter.PadKeyPaintArgs GetPadKeyPaintArgs(PadKeyPainter.PadKeyKind kind)
        {
            int key = KeyIndexOfKind(kind);
            int selected = SensorIndex / SENSORS_PER_KEY;
            Color cellBg = pal.CardBg;
            Color outline = pal.PadOutline;
            bool triggered = key >= 0 && key < padKeyActive.Length && padKeyActive[key];
            Color triggerFill = GetPadLedColor(key);
            if (triggered)
                triggerFill = Color.FromArgb(220, triggerFill);
            return new PadKeyPainter.PadKeyPaintArgs
            {
                CellBackground = cellBg,
                OutlineColor = (key == selected) ? Color.White : outline,
                SelectedFill = pal.Selected,
                SelectedGradientEnd = pal.SelectedGradientEnd,
                GlowColor = pal.Glow,
                ArrowTip = pal.ArrowTip,
                ArrowMid = pal.ArrowMid,
                ArrowBack = pal.ArrowBack,
                ArrowGlow = pal.ArrowGlow,
                BorderColor = pal.Border,
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
            if (menuLanguage == null || btnLangToggle == null)
                return;
            RefreshLanguageMenu();
            menuLanguage.Show(btnLangToggle, new Point(0, btnLangToggle.Height));
        }

        /// <summary>
        /// Every user-visible caption of the main window. All text comes from Strings*.resx via L;
        /// called at start-up and again whenever the language changes (theme-independent, so the
        /// dark palette simply recolours the same strings).
        /// </summary>
        private void ApplyUiLanguage()
        {
            if (btnLangToggle != null) btnLangToggle.Text = L.Get("Btn.Language");
            btn_default.Text = L.Get("Btn.Default");
            btn_Read.Text = L.Get("Btn.Read");
            btn_Save.Text = L.Get("Btn.Save");
            Btn_ReadMode.Text = L.Get("Btn.ReadMode");
            Btn_SaveMode.Text = L.Get("Btn.SaveMode");
            button1.Text = L.Get("Btn.RawRead");
            button2.Text = L.Get("Btn.RawClear");
            if (btnFirmwareUpgrade != null) btnFirmwareUpgrade.Text = L.Get("Btn.FwUpgrade");
            groupBox_Button.Text = L.Get("Group.Edit");
            groupBox_Mode.Text = L.Get("Group.Mode");
            groupBox_Sensi.Text = L.Get("Group.Sensitivity");
            groupBox_Color.Text = L.Get("Group.Color");
            groupBox_DTick.Text = L.Get("Group.Debounce");
            groupBox2.Text = L.Get("Group.PlayerNum");
            label_DTick.Text = L.Get("Label.Hold");
            tabPage_Sensi.Text = L.Get("Tab.Sensitivity");
            tabPage_Color.Text = L.Get("Tab.Color");
            tabPage_DTick.Text = L.Get("Tab.Debounce");
            groupBox_USBMode.Text = L.Get("Group.USB");
            radioButton_KBS.Text = L.Get("Radio.Keyboard");
            radioButton_HID.Text = L.Get("Radio.Gamepad");
            radioButton_IO4.Text = L.Get("Radio.IO");
            radioButton_1P.Text = L.Get("Radio.1P");
            radioButton_2P.Text = L.Get("Radio.2P");
            if (tabPage_Map != null) tabPage_Map.Text = L.Get("Tab.KeyMap");
            if (groupBox_Map != null) groupBox_Map.Text = L.Get("Map.Group");
            if (label_MapHint != null) label_MapHint.Text = L.Get("Map.Hint");
            if (label_MapKey != null) label_MapKey.Text = L.Get("Map.KeyLabel");
            if (btn_MapRead != null) btn_MapRead.Text = L.Get("Map.Read");
            if (btn_MapSave != null) btn_MapSave.Text = L.Get("Map.Save");
            if (btn_MapDefault != null) btn_MapDefault.Text = L.Get("Map.Default");
            if (mapKeyLabels != null)
                for (int i = 0; i < mapKeyLabels.Length; i++)
                    mapKeyLabels[i].Text = L.Get(PadKeyCaptionKey(i));
            RelocalizeHidUsageCombos();
            UpdateDebugButtonText();
            UpdateThemeToggleText();
            UpdateWindowTitle();
            UpdateVersionLabel();
            RefreshLanguageMenu();
        }

        private void UpdateDebugButtonText()
        {
            Btn_ADJ.Text = L.Get(bDebugModesFG ? "Btn.Stop" : "Btn.Debug");
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
            DebugLog.Log("TX slot{0} SET_MRAW: {1}", mDeviceIndex, DebugLog.Hex(SendByteBuf, 0, index));
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, SendByteBuf);
        }

        /// <summary>NOLMD — stop the RAW stream on the board without touching the host-side flag/button.</summary>
        private void SendRawStreamOff()
        {
            SendRawModeCommand(0x4E, 0x4F, 0x4C, 0x4D, 0x44); // NOLMD
        }

        private void SetDeviceRawDebugMode(bool enable)
        {
            if (enable)
                SendRawModeCommand(0x52, 0x41, 0x57, 0x4D, 0x44); // RAWMD
            else
                SendRawStreamOff();
            bDebugModesFG = enable;
            UpdateDebugButtonText();
        }

        /// <summary>
        /// Send a command, wait, read the 64-byte input report back. The buffer is pre-filled with
        /// 0xFF so a failed HidD_GetInputReport (HIDLibrary leaves the buffer untouched) can be told
        /// apart from a genuine all-zero reply. Everything is logged.
        /// </summary>
        private ReplyKind Transact(string what, byte[] txBuf, int txLen, byte expectedCmd, int waitMs, byte[] rxBuf)
        {
            byte ReportSize = 64;
            byte ReportID = 0;
            DebugLog.Log("TX slot{0} {1}: {2}", mDeviceIndex, what, DebugLog.Hex(txBuf, 0, txLen));
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportID, ReportSize, txBuf);
            Thread.Sleep(waitMs);
            return ReadReply(what, expectedCmd, rxBuf);
        }

        private ReplyKind ReadReply(string what, byte expectedCmd, byte[] rxBuf)
        {
            byte ReportSize = 64;
            byte ReportID = 0;
            DeviceProtocol.FillSentinel(rxBuf);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportID, ReportSize, rxBuf);
            ReplyKind kind = DeviceProtocol.Classify(rxBuf, 0, expectedCmd);
            DebugLog.Log("RX slot{0} {1} -> {2}: {3}", mDeviceIndex, what, kind, DebugLog.Hex(rxBuf, 0, ReportSize));
            return kind;
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
            // LANGUAGE is applied in Program.Main (before any form exists); THEME is read here.
            // NAME from Config.ini is no longer used for the title: it carried a stale version
            // string ("PADUEX5 V1.0.0.44") that was shown next to the real assembly version.
            string theme = AppConfig.Read("THEME");
            bDarkMode = theme != null && theme.Trim().Equals("DARK", StringComparison.OrdinalIgnoreCase);
            // KEYCOUNT=4 → 4-key cross layout (PADUEX4); anything else → 5-key (PADUEX5)
            string keys = AppConfig.Read("KEYCOUNT");
            KeyCount = keys != null && keys.Trim() == "4" ? 4 : KEY_COUNT_5KEY;
            DebugLog.Log("KEYCOUNT={0} -> {1}-key layout ({2})", keys ?? "(unset)", KeyCount, AppName);
            ///------------------------------------------------------------------------------------------
            UpdateWindowTitle();
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
            // Leaving the board in RAW mode makes every GET reply of the *next* session get
            // overwritten by "AMRAW" frames (version / sensitivity unreadable until re-plug).
            if (bDeviceStatusFG && bDebugModesFG)
            {
                try { SendRawStreamOff(); } catch { }
                bDebugModesFG = false;
            }
            DebugLog.Log("==== exit ====");
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
            // Forget the previous board completely: version, connection state, label.
            bDeviceStatusFG = false;
            mConnState = ConnState.None;
            ResetDeviceVersion();
            UpdateWindowTitle();
            UpdateVersionLabel();
            SetModeGroupEnabled(false);
            bool slot0 = false, slot1 = false;
            int pid0 = 0, pid1 = 0;
            //-------------------------------------------------------------------------------------------//V1.0.0.20:20241231(1)Display OTA Device infor on App Title
            if (USBCtrl.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                mConnState = ConnState.Ota;
                UpdateWindowTitle();
                DebugLog.Log("USB: OTA bootloader 0E8F:8901 connected");
                return;
            }
            //-------------------------------------------------------------------------------------------
            if (((pid0 = USBCtrl.USBLib_Connect((int)DeviceType.DDR_KBS_VID, (int)DeviceType.DDR_KBS_PID1, (int)DeviceType.None)) != (int)DeviceType.None) ||
                ((pid0 = USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.DDR_HID_PID1, (int)DeviceType.None)) != (int)DeviceType.None))      //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
            {
                bDeviceStatusFG = true;
                slot0 = true;
            }
            //-------------------------------------------------------------------------------------------
            if (((pid1 = USBCtrl.USBLib_Connect((int)DeviceType.DDR_KBS_VID, (int)DeviceType.None, (int)DeviceType.DDR_KBS_PID2)) != (int)DeviceType.None) ||
                ((pid1 = USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.None, (int)DeviceType.DDR_HID_PID2)) != (int)DeviceType.None) ||    //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
                ((pid1 = USBCtrl.USBLib_Connect((int)DeviceType.DDR_HID_VID, (int)DeviceType.None, (int)DeviceType.DDR_HID_PID3)) != (int)DeviceType.None))      //V1.0.0.32:20250529(1)DDR HID Mode PID compatibility Issue
            {
                bDeviceStatusFG = true;
                slot1 = true;
            }
            //-------------------------------------------------------------------------------------------
            if (bDeviceStatusFG == true)
            {
                mConnState = ConnState.Device;
                UpdateWindowTitle();
                SetModeGroupEnabled(true);
                //---------------------------------------------------------------------------------------
                // HIDLibrary opens PID1 devices in slot 0 and PID2/PID3 devices in slot 1.
                mDeviceIndex = (byte)(slot0 ? 0 : (slot1 ? 1 : 0));
                DebugLog.Log("USB: connected VID 4D47, slot0 PID {0:X4}, slot1 PID {1:X4} -> using slot{2}",
                    pid0, pid1, mDeviceIndex);
                //---------------------------------------------------------------------------------------
                Btn_ReadMode_Click(null, null);
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
            else
            {
                DebugLog.Log("USB: no device");
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
            // paduex3_1.0.bin + fixed source: DTick[(i/4)*4]; V2.12 quirk: DTick[key].
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
            // Start-up fills every slot. Afterwards only the keys this build shows are reset: in
            // 4-key mode the K5 slots (16..19, plus DTick quirk slot 4) keep what was last read
            // from the board so "Save" writes them back unchanged.
            bool startup = !layoutReady;
            for (int i = 0; i < CONFIG_VALUE_COUNT; i++)
            {
                if (!startup && !IsVisibleKey(i / SENSORS_PER_KEY)) continue;
                SensiBuffer[i] = DEFAULT_SENSI_TAB[i];
                if (!IsMapSlot(i)) ColorBuffer[i] = DEFAULT_COLOR_TAB[i];   // slot +1 = keymap (paduex3_1.7)
                if (startup || IsVisibleKey(i)) DTickBuffer[i] = DEFAULT_DTICK_TAB[i];
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
        private static string HexHead(byte[] buf, int start, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(buf[start + i].ToString("X02"));
            }
            return sb.ToString();
        }

        private byte Read_Config_Value(byte ConfigType, int[] ConfigOut, int Len)
        {
            //-------------------------------------------------------------------------------------------
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
            //-------------------------------------------------------------------------------------------
            // Same shared reply buffer as GET_MODE: a board left in RAW mode overwrites the reply,
            // so detect the "AMRAW" frame, stop the stream and ask again instead of failing at once.
            ReplyKind kind = ReplyKind.NoData;
            bool header = false;
            for (int attempt = 1; attempt <= GET_SENS_RETRIES && !header; attempt++)
            {
                kind = Transact("GET_SENS type " + ConfigType.ToString("X2") + " #" + attempt, SendByteBuf, index,
                    DeviceProtocol.CMD_GET_SENS, 100, InputReportBuf);
                header = DeviceProtocol.IsSensReplyHeader(InputReportBuf, ReportIndex, ConfigType);
                if (!header && kind == ReplyKind.RawStream)
                {
                    DebugLog.Log("GET_SENS: board is in RAW debug mode, sending NOLMD");
                    SendRawStreamOff();
                    Thread.Sleep(80);
                }
            }
            //-------------------------------------------------------------------------------------------
            if (header)
            {
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
                    DebugLog.Log("GET_SENS type {0:X2}: OK crc={1:X2}", ConfigType, CS);
                }
                else
                {
                    DebugLog.Log("GET_SENS type {0:X2}: CRC error calc={1:X2} got={2:X2}", ConfigType, CS, InputReportBuf[ReportIndex + PktLen - 1]);
                    MessageBox.Show(L.F("Cfg.ReadCsError", CS.ToString("X02"), InputReportBuf[ReportIndex + PktLen - 1].ToString("X02")), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 0;
                }
                //---------------------------------------------------------------------------------------
            }
            else
            {
                DebugLog.Log("GET_SENS type {0:X2}: FAILED, last={1}", ConfigType, kind);
                string why = kind == ReplyKind.RawStream ? L.Get("Ver.Fail.RawMode")
                           : kind == ReplyKind.NoData ? L.Get("Ver.Fail.HidRead")
                           : kind == ReplyKind.Empty ? L.Get("Ver.Fail.NoReply")
                           : HexHead(InputReportBuf, ReportIndex, 5);
                MessageBox.Show(L.F("Cfg.ReadError", why), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            MessageBox.Show(L.Get("Cfg.ReadOk"), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            ReplyKind kind = Transact("SET_SENS type " + ConfigType.ToString("X2"), SendByteBuf, index,
                DeviceProtocol.CMD_SET_SENS, 100, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (DeviceProtocol.IsSensAckHeader(InputReportBuf, ReportIndex, ConfigType))
            {
                DebugLog.Log("SET_SENS type {0:X2}: OK (ack crc={1:X2}, sent crc={2:X2})", ConfigType, InputReportBuf[ReportIndex + 3], Txd_CS);
            }
            else
            {
                DebugLog.Log("SET_SENS type {0:X2}: FAILED, reply kind={1}", ConfigType, kind);
                string why = kind == ReplyKind.RawStream ? L.Get("Ver.Fail.RawMode")
                           : kind == ReplyKind.NoData ? L.Get("Ver.Fail.HidRead")
                           : kind == ReplyKind.Empty ? L.Get("Ver.Fail.NoReply")
                           : HexHead(InputReportBuf, ReportIndex, 5);
                MessageBox.Show(L.F("Cfg.WriteError", why), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            MessageBox.Show(L.Get("Cfg.WriteOk"), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_R) { Btn_LED.BackColor = System.Drawing.Color.Red; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_G) { Btn_LED.BackColor = System.Drawing.Color.Green; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_B) { Btn_LED.BackColor = System.Drawing.Color.Blue; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_Y) { Btn_LED.BackColor = System.Drawing.Color.Yellow; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_P) { Btn_LED.BackColor = System.Drawing.Color.Fuchsia; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_C) { Btn_LED.BackColor = System.Drawing.Color.Cyan; }
                else if (ColorBuffer[(SensorIndex / 4) * 4] == (byte)LED_COLOR_W) { Btn_LED.BackColor = System.Drawing.Color.White; }
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
                MessageBox.Show(L.F("Msg.InputError", ex.Message), L.Get("Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(L.F("Msg.InputError", ex.Message), L.Get("Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(L.F("Msg.InputError", ex.Message), L.Get("Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(L.F("Msg.InputError", ex.Message), L.Get("Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// =============================================================================================
        /// ====  Btn Sensor LED Click  ====
        /// =============================================================================================
        ///
        private void Btn_Col_R_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_R;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_R;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_R;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_G_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_G;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_G;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_G;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_B_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_B;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_B;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_B;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_Y_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_Y;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_Y;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_Y;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_P_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_P;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_P;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_P;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_C_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_C;
            ColorBuffer[(SensorIndex / 4) * 4 + 2] = LED_COLOR_C;
            ColorBuffer[(SensorIndex / 4) * 4 + 3] = LED_COLOR_C;
            Sensor_Panel_Display_ReFlash(); 
        }

        private void Btn_Col_W_Click(object sender, EventArgs e)
        {
            ColorBuffer[(SensorIndex / 4) * 4 + 0] = LED_COLOR_W;
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
                MessageBox.Show(L.F("Msg.InputError", ex.Message), L.Get("Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            ReadDeviceMode();
        }

        /// <summary>
        /// GET_MODE (0x47). Reply (64-byte input report, read with HidD_GetInputReport; HIDLibrary
        /// strips the Report-ID byte, so [0] is the command):
        ///   [0]=0x47  [1]=mode  [2]=UID 0xA2  [3]=HVer 0x13  [4]=FW major  [5]=FW minor (plain integers)
        /// The firmware only writes this reply into its shared 64-byte reply buffer (AppUser+0x11E),
        /// which the host fetches with GET_REPORT. While the board is in RAW debug mode it rewrites
        /// that buffer with "AMRAW" frames on every scan, so the reply is gone before we read it —
        /// and the board stays in RAW mode until NOLMD or re-plug, even across host restarts.
        /// Strategy ("show something rather than nothing"):
        ///   * accept any frame whose first byte is 0x47; UID/HVer only annotate the label,
        ///   * on a RAW frame send NOLMD and ask again,
        ///   * re-send GET_MODE on every retry, 100 ms apart,
        ///   * remember *why* it failed and show that in the label; log every byte.
        /// </summary>
        private bool ReadDeviceMode()
        {
            //-------------------------------------------------------------------------------------------
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            ResetDeviceVersion();
            UpdateVersionLabel();
            if (!bDeviceStatusFG)
            {
                DebugLog.Log("GET_MODE: skipped, no device");
                return false;
            }
            //-------------------------------------------------------------------------------------------
            bool wasDebug = bDebugModesFG;
            if (wasDebug)
            {
                SetDeviceRawDebugMode(false);
                Thread.Sleep(80);
            }
            ModeReply reply = null;
            ReplyKind last = ReplyKind.NoData;
            bool sawRaw = false;
            try
            {
                SendByteBuf[0] = (byte)UserCommand.GET_MODE;
                for (int attempt = 1; attempt <= GET_MODE_RETRIES && reply == null; attempt++)
                {
                    last = Transact("GET_MODE #" + attempt, SendByteBuf, 1, DeviceProtocol.CMD_GET_MODE, GET_MODE_WAIT_MS, InputReportBuf);
                    if (last == ReplyKind.Match)
                    {
                        reply = DeviceProtocol.ParseModeReply(InputReportBuf, 0);
                        break;
                    }
                    if (last == ReplyKind.RawStream)
                    {
                        // Board is streaming RAW (left over from an earlier session): stop it first.
                        sawRaw = true;
                        DebugLog.Log("GET_MODE: board is in RAW debug mode, sending NOLMD");
                        SendRawStreamOff();
                        Thread.Sleep(80);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("GET_MODE: exception {0}", ex);
            }
            finally
            {
                if (wasDebug)
                {
                    Thread.Sleep(80);
                    SetDeviceRawDebugMode(true);
                }
            }
            //-------------------------------------------------------------------------------------------
            if (reply == null)
            {
                switch (last)
                {
                    case ReplyKind.NoData: mFwFailReasonKey = "Ver.Fail.HidRead"; break;
                    case ReplyKind.Empty: mFwFailReasonKey = "Ver.Fail.NoReply"; break;
                    case ReplyKind.RawStream: mFwFailReasonKey = "Ver.Fail.RawMode"; break;
                    default: mFwFailReasonKey = "Ver.Fail.BadFrame"; break;
                }
                DebugLog.Log("GET_MODE: FAILED after {0} attempts, last={1}, reason={2}", GET_MODE_RETRIES, last, mFwFailReasonKey);
                UpdateVersionLabel();   // shows the reason instead of a stale value
                return false;
            }
            //-------------------------------------------------------------------------------------------
            byte mode = reply.Mode;
            mDevicePlayerBits = (byte)(mode & (byte)UserMode.MSEL_MAK);
            byte usbMode = (byte)(mode & (byte)UserMode.MODE_MAK);
            radioButton_KBS.Checked = usbMode == (byte)UserMode.DDR_KBS;
            radioButton_HID.Checked = usbMode == (byte)UserMode.DDR_HID;
            radioButton_IO4.Checked = usbMode == (byte)UserMode.DDR_IO4;
            //-------------------------------------------------------------------------------------------
            mDevUid = reply.Uid;
            mDevHVer = reply.HVer;
            mFwMajor = reply.FwMajor;
            mFwMinor = reply.FwMinor;
            mFwValid = true;
            DebugLog.Log("GET_MODE: OK mode=0x{0:X2} UID={1:X2}{2} HVer={3:X2}{4} FVer={5}{6}",
                mode, mDevUid, reply.UidMismatch ? "(!)" : "", mDevHVer, reply.HVerUnknown ? "(!)" : "",
                FwVersionText(), sawRaw ? " (recovered from RAW mode)" : "");
            UpdateVersionLabel();
            return true;
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
            byte WorkMode = mDevicePlayerBits;
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
            DebugLog.Log("TX slot{0} SET_MODE: {1} (MCU reboots)", mDeviceIndex, DebugLog.Hex(SendByteBuf, 0, index));
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
