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
        // Live key lamps, one row per USB slot. Slot 0 is the 1P PID, slot 1 is the 2P PID.
        private readonly bool[][] padKeyActive = new bool[][] { new bool[5], new bool[5] };
        private byte mDeviceIndex = 0;
        // Reference V1.21 ships as two builds that enumerate as different PIDs (1P in slot 0,
        // 2P in slot 1). Both slots stay open. mDeviceIndex is only which pad Read/Save talk to.
        // The mode byte's player bit is still echoed back; V2.12+ firmware ignores it.
        private byte mDevicePlayerBits = (byte)UserMode.DDR_2PS;
        private readonly bool[] slotOnline = new bool[2];
        private readonly ModeReply[] slotCache = new ModeReply[2];
        private readonly string[] slotVersion = new string[2];
        private Button[] pad2Buttons;
        private Label[] padCaptions;
        private Panel[] fiveKeyFillers2;
        private readonly int[] padOriginX = new int[2];
        private int padBlockW;
        private int padEditTop;

        // Device identity from the last successful GET_MODE. mFwValid is cleared on
        // disconnect / OTA / failed read so a stale version is never shown or used.
        private bool mFwValid = false;
        private int mDevUid = 0, mDevHVer = 0;
        private int mFwMajor = 0, mFwMinor = 0;
        // Resource key describing why the last GET_MODE failed ("" = unknown).
        private string mFwFailReasonKey = "";
        // GET_MODE is re-sent a few times, 100 ms apart. A board left in RAW debug mode
        // overwrites every reply with "AMRAW" frames; on such a frame NOLMD is sent first.
        private const int GET_MODE_RETRIES = 6;
        private const int GET_MODE_WAIT_MS = 100;
        private const int GET_SENS_RETRIES = 3;

        /// <summary>
        /// Firmware version bytes are plain integers. V3.0 and later are shown as "V3.0";
        /// the older 2.x images keep their original two-digit minor ("2.12", "2.17").
        /// </summary>
        private string FwVersionText()
        {
            if (mFwMajor >= 3) return "V" + mFwMajor + "." + mFwMinor;
            return mFwMajor.ToString() + "." + mFwMinor.ToString("D02");
        }

        // Known firmware versions:
        //   3.2  current release (HVer 0x13 = 5-key PADUEX5, 0x14 = 4-key PADUEX4)
        //   3.1  SET_SENS(sensitivity) wrote the flash page, but boot still reset the table to 200
        //   3.0  first V3 release; SET_SENS(sensitivity) only reached RAM
        //   2.17 previous 5-key image, 2.18 previous 4-key image
        //   2.12 stock V2.12 / V2.13 (stock never bumped its minor constant)
        // Anything else is shown as-is. UID / HVer mismatches are annotated, never rejected.
        private string FwTag()
        {
            if (!mFwValid) return "";
            string tag = "";
            if (mFwMajor >= 3)
            {
                if (mDevHVer == DeviceProtocol.HVER_4KEY) tag = L.Get("Ver.Tag.Board4");
                else if (mDevHVer == DeviceProtocol.HVER_5KEY) tag = L.Get("Ver.Tag.Board5");
                if (!FwPersistsSensitivity()) tag += L.Get("Ver.Tag.UpgradeSensi");
            }
            else if (mFwMajor == 2 && mFwMinor == 17) tag = L.Get("Ver.Tag.Paduex5_17");
            else if (mFwMajor == 2 && mFwMinor == 18) tag = L.Get("Ver.Tag.Paduex4_10");
            else if (mFwMajor == 2 && mFwMinor == 12) tag = L.Get("Ver.Tag.Stock212");
            if (mDevUid != DeviceProtocol.ID_DDR) tag += L.Get("Ver.Tag.UidMismatch");
            if (!DeviceProtocol.IsKnownHVer(mDevHVer)) tag += L.Get("Ver.Tag.HVerUnknown");
            return tag;
        }
        private bool FwHasKeyMap()
        {
            return mFwValid && (mFwMajor >= 3 || (mFwMajor == 2 && mFwMinor >= 17));
        }
        /// <summary>
        /// Stock V2.13 and every image up to V3.0 never write the sensitivity page; V3.1 writes
        /// it but its boot code still overwrites the loaded table with 200. On all of them the
        /// sensitivity reads back as 200 after a power cycle. V3.2 keeps the saved values.
        /// </summary>
        private bool FwPersistsSensitivity()
        {
            return mFwValid && (mFwMajor > 3 || (mFwMajor == 3 && mFwMinor >= 2));
        }

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
        private static readonly string HostVer = "V" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
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
        ///   [    ] [1 ↑ ] [    ]
        ///   [2 ← ] [    ] [3 → ]
        ///   [    ] [4 ↓ ] [    ]
        /// Firmware key index 0..3 (Btn_K1..K4) → arrow. Key index 4 (K5) is never shown; its 4
        /// config slots are still transferred unchanged so the 20-value packets stay intact.
        /// Mapping K1=↑, K2=←, K3=→, K4=↓ confirmed on the real 4-key pad (2026-11-05).
        /// Arrow colours follow the direction, not the K number (see FourKeyArrowColors).
        /// =============================================================================================
        private static readonly PadKeyPainter.PadKeyKind[] FourKeyKinds = new PadKeyPainter.PadKeyKind[] {
            PadKeyPainter.PadKeyKind.Up, PadKeyPainter.PadKeyKind.Left,
            PadKeyPainter.PadKeyKind.Right, PadKeyPainter.PadKeyKind.Down
        };

        private static string FourKeyCaptionKey(PadKeyPainter.PadKeyKind kind)
        {
            switch (kind)
            {
                case PadKeyPainter.PadKeyKind.Up: return "Map.Pad.U";
                case PadKeyPainter.PadKeyKind.Down: return "Map.Pad.D";
                case PadKeyPainter.PadKeyKind.Left: return "Map.Pad.L";
                default: return "Map.Pad.R";
            }
        }

        private static Point FourKeyCell(PadKeyPainter.PadKeyKind kind, int x0, int x1, int x2, int y0, int y1, int y2)
        {
            switch (kind)
            {
                case PadKeyPainter.PadKeyKind.Up: return new Point(x1, y0);
                case PadKeyPainter.PadKeyKind.Down: return new Point(x1, y2);
                case PadKeyPainter.PadKeyKind.Left: return new Point(x0, y1);
                default: return new Point(x2, y1);
            }
        }

        // 4-key arrow colours: idle white (dark theme) / dark grey (light theme); pressed = red for
        // left/right, green for up/down. Same 3-layer chevron + glow, only the colours change.
        private static readonly Color FourKeyPressRed = Color.FromArgb(0xEF, 0x44, 0x44);
        private static readonly Color FourKeyPressGreen = Color.FromArgb(0x22, 0xC5, 0x5E);

        private static Color FourKeyPressColor(PadKeyPainter.PadKeyKind kind)
        {
            return kind == PadKeyPainter.PadKeyKind.Left || kind == PadKeyPainter.PadKeyKind.Right
                ? FourKeyPressRed : FourKeyPressGreen;
        }

        private void FourKeyIdleColors(out Color tip, out Color mid, out Color back, out Color glow)
        {
            if (bDarkMode)
            {
                tip = Color.White;
                mid = Color.FromArgb(0xE5, 0xE7, 0xEB);
                back = Color.FromArgb(0x9C, 0xA3, 0xAF);
                glow = Color.White;
            }
            else
            {
                tip = Color.FromArgb(0x4B, 0x55, 0x63);
                mid = Color.FromArgb(0x37, 0x41, 0x51);
                back = Color.FromArgb(0x1F, 0x29, 0x37);
                glow = Color.FromArgb(0x9C, 0xA3, 0xAF);
            }
        }

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
            return FourKeyMode && k < FourKeyKinds.Length ? FourKeyCaptionKey(FourKeyKinds[k]) : MapPadKeys[k];
        }
        // Design canvas at 96 DPI — all layout math uses S(designPx).
        // Wide enough for the 1P pad and the 2P pad side by side, plus the existing tabs.
        private const int DesignW = 1480;
        private const int DesignH = 800;
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
        // Firmware default tables. 1P: W X A D S   2P: H K J N M
        private readonly int[] DefaultKeyMapping1P = new int[] { 0x1A, 0x1B, 0x04, 0x07, 0x16 };
        private readonly int[] DefaultKeyMapping2P = new int[] { 0x0B, 0x0E, 0x0D, 0x11, 0x10 };
        // Key map lives in the otherwise unused Color[key*4+1] slot, encoded 0x4B00 ("K") | usage.
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
            EnsureDualPads();
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

            // Two pads, same 3×3 cell as before. 1P is the left grid (USB slot 0), 2P the right.
            int cell = S(140);
            int gap = S(6);
            int ox = S(12);
            int oy = S(36);
            int padW;
            int padBottom = PlacePlayerPad(0, ox, oy, cell, gap, out padW);
            int pad2X = ox + padW + S(18);
            int padW2;
            PlacePlayerPad(1, pad2X, oy, cell, gap, out padW2);
            int padsRight = pad2X + padW2;
            padOriginX[0] = ox;
            padOriginX[1] = pad2X;
            padBlockW = padW;
            padEditTop = padBottom + S(10);
            InvalidatePadKeyButtons();

            // Edit box stays the original single-pad size and sits under the pad Read/Save will use.
            PlaceEditGroup();

            // Tabs — leave margin so right edge never clips
            int tabX = padsRight + S(16);
            int tabW = S(DesignW - 12) - tabX; // fill to design right margin
            if (tabW < S(420)) tabW = S(420);
            int tabH = (padBottom - oy) + S(10) + groupBox_Button.Height;
            tabControl1.Location = new Point(tabX, oy);
            tabControl1.Size = new Size(tabW, tabH);
            if (tabControl1.DrawMode == TabDrawMode.OwnerDrawFixed && tabControl1.TabPages.Count > 0)
                tabControl1.ItemSize = new Size(Math.Max(S(80), (tabW - S(6)) / tabControl1.TabPages.Count), S(TabStripH));

            // Mode full width under content. Extra height holds the selected pad's version line.
            int modeY = Math.Max(groupBox_Button.Bottom, tabControl1.Bottom) + S(10);
            int modeW = tabX + tabW - ox;
            groupBox_Mode.Location = new Point(ox, modeY);
            groupBox_Mode.Size = new Size(modeW, S(124));

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
            label_Ver.AutoSize = false;
            label_Ver.AutoEllipsis = true;
            label_Ver.Location = new Point(ox + S(16), modeY + S(86));
            label_Ver.Size = new Size(Math.Max(S(200), modeW - S(32)), S(28));
            label_Ver.BringToFront();
        }

        /// <summary>
        /// Place one player grid. Returns the bottom of the grid. padWidth is the 3-cell width.
        /// Player 0 uses the designer buttons; player 1 uses the buttons created in EnsureDualPads.
        /// </summary>
        private int PlacePlayerPad(int player, int originX, int oy, int cell, int gap, out int padWidth)
        {
            int step = cell + gap;
            int x0 = originX, x1 = originX + step, x2 = originX + step * 2;
            int y0 = oy, y1 = oy + step, y2 = oy + step * 2;
            padWidth = step * 3 - gap;
            Size cellSize = new Size(cell, cell);
            Button[] keys = player == 0
                ? new Button[] { Btn_K1, Btn_K2, Btn_K3, Btn_K4, Btn_K5 }
                : pad2Buttons;

            Point[] fillerPos;
            if (FourKeyMode)
            {
                keys[0].Location = FourKeyCell(FourKeyKinds[0], x0, x1, x2, y0, y1, y2);
                keys[1].Location = FourKeyCell(FourKeyKinds[1], x0, x1, x2, y0, y1, y2);
                keys[2].Location = FourKeyCell(FourKeyKinds[2], x0, x1, x2, y0, y1, y2);
                keys[3].Location = FourKeyCell(FourKeyKinds[3], x0, x1, x2, y0, y1, y2);
                keys[4].Location = new Point(x2, y2);
                keys[4].Enabled = false;
                keys[4].Visible = false;
                fillerPos = new Point[] {
                    new Point(x0, y0), new Point(x2, y0), new Point(x1, y1), new Point(x0, y2), new Point(x2, y2)
                };
            }
            else
            {
                keys[0].Location = new Point(x0, y0);
                keys[1].Location = new Point(x2, y0);
                keys[2].Location = new Point(x1, y1);
                keys[3].Location = new Point(x0, y2);
                keys[4].Location = new Point(x2, y2);
                keys[4].Enabled = true;
                keys[4].Visible = true;
                fillerPos = new Point[] {
                    new Point(x1, y0), new Point(x0, y1), new Point(x2, y1), new Point(x1, y2)
                };
            }
            for (int i = 0; i < 4; i++)
            {
                keys[i].Size = cellSize;
                keys[i].Enabled = true;
                keys[i].Visible = true;
            }
            keys[4].Size = cellSize;

            Panel[] fillers = player == 0 ? fiveKeyFillers : fiveKeyFillers2;
            if (fillers == null || fillers.Length != fillerPos.Length)
            {
                if (fillers != null)
                {
                    foreach (Panel old in fillers)
                    {
                        this.Controls.Remove(old);
                        old.Dispose();
                    }
                }
                fillers = new Panel[fillerPos.Length];
                for (int i = 0; i < fillerPos.Length; i++)
                {
                    Panel p = new Panel();
                    p.BackColor = Color.FromArgb(245, 245, 245);
                    p.BorderStyle = BorderStyle.None;
                    p.Enabled = false;
                    p.Paint += FillerPanel_Paint;
                    this.Controls.Add(p);
                    p.SendToBack();
                    fillers[i] = p;
                }
                if (player == 0) fiveKeyFillers = fillers;
                else fiveKeyFillers2 = fillers;
            }
            for (int i = 0; i < fillers.Length; i++)
            {
                fillers[i].Location = fillerPos[i];
                fillers[i].Size = cellSize;
                fillers[i].Visible = true;
            }

            if (padCaptions != null && player < padCaptions.Length && padCaptions[player] != null)
            {
                padCaptions[player].Location = new Point(originX + S(4), S(6));
                padCaptions[player].BringToFront();
            }
            return y2 + cell;
        }

        private void PlaceEditGroup()
        {
            if (groupBox_Button == null || padBlockW <= 0)
                return;
            int player = mDeviceIndex == 1 ? 1 : 0;
            groupBox_Button.Location = new Point(padOriginX[player], padEditTop);
            groupBox_Button.Size = new Size(padBlockW, S(130));
            Size editBtn = new Size(padBlockW / 2 - S(24), S(36));
            btn_default.Location = new Point(S(14), S(26));
            Btn_ADJ.Location = new Point(S(14) + padBlockW / 2, S(26));
            btn_Read.Location = new Point(S(14), S(72));
            btn_Save.Location = new Point(S(14) + padBlockW / 2, S(72));
            btn_default.Size = Btn_ADJ.Size = btn_Read.Size = btn_Save.Size = editBtn;
            groupBox_Button.BringToFront();
        }

        private void EnsureDualPads()
        {
            if (pad2Buttons != null)
                return;
            pad2Buttons = new Button[KEY_COUNT_5KEY];
            for (int i = 0; i < KEY_COUNT_5KEY; i++)
            {
                int key = i;
                Button b = new Button();
                b.Name = "Btn_P2_K" + (i + 1);
                b.TabStop = false;
                b.Click += delegate { SelectPadKey(1, key); };
                this.Controls.Add(b);
                pad2Buttons[i] = b;
            }
            padCaptions = new Label[2];
            for (int p = 0; p < 2; p++)
            {
                Label lb = new Label();
                lb.Name = p == 0 ? "label_Pad1P" : "label_Pad2P";
                lb.AutoSize = true;
                lb.BackColor = Color.Transparent;
                this.Controls.Add(lb);
                padCaptions[p] = lb;
            }
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
            Panel[][] fillerSets = new Panel[][] { fiveKeyFillers, fiveKeyFillers2 };
            foreach (Panel[] set in fillerSets)
            {
                if (set == null) continue;
                foreach (Panel p in set)
                {
                    if (p == null) continue;
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
            if (padCaptions != null)
            {
                foreach (Label lb in padCaptions)
                {
                    if (lb == null) continue;
                    lb.BackColor = pal.FormBg;
                    lb.ForeColor = pal.Title;
                    UiFont.Set(lb, UiFont.Status, FontStyle.Bold);
                }
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
            EnsureDualPads();
            if (FourKeyMode)
            {
                PadKeyPainter.WireButton(Btn_K1, FourKeyKinds[0], 0);
                PadKeyPainter.WireButton(Btn_K2, FourKeyKinds[1], 0);
                PadKeyPainter.WireButton(Btn_K3, FourKeyKinds[2], 0);
                PadKeyPainter.WireButton(Btn_K4, FourKeyKinds[3], 0);
                for (int i = 0; i < 4; i++)
                    PadKeyPainter.WireButton(pad2Buttons[i], FourKeyKinds[i], 1);
            }
            else
            {
                PadKeyPainter.WireButton(Btn_K1, PadKeyPainter.PadKeyKind.UpperLeft, 0);
                PadKeyPainter.WireButton(Btn_K2, PadKeyPainter.PadKeyKind.UpperRight, 0);
                PadKeyPainter.WireButton(Btn_K3, PadKeyPainter.PadKeyKind.Center, 0);
                PadKeyPainter.WireButton(Btn_K4, PadKeyPainter.PadKeyKind.DownLeft, 0);
                PadKeyPainter.WireButton(pad2Buttons[0], PadKeyPainter.PadKeyKind.UpperLeft, 1);
                PadKeyPainter.WireButton(pad2Buttons[1], PadKeyPainter.PadKeyKind.UpperRight, 1);
                PadKeyPainter.WireButton(pad2Buttons[2], PadKeyPainter.PadKeyKind.Center, 1);
                PadKeyPainter.WireButton(pad2Buttons[3], PadKeyPainter.PadKeyKind.DownLeft, 1);
            }
            PadKeyPainter.WireButton(Btn_K5, PadKeyPainter.PadKeyKind.DownRight, 0);
            PadKeyPainter.WireButton(pad2Buttons[4], PadKeyPainter.PadKeyKind.DownRight, 1);
            padKeyVectorWired = true;
        }

        internal PadKeyPainter.PadKeyPaintArgs GetPadKeyPaintArgs(PadKeyPainter.PadKeyKind kind, int player)
        {
            int key = KeyIndexOfKind(kind);
            int selected = SensorIndex / SENSORS_PER_KEY;
            Color cellBg = pal.CardBg;
            Color outline = pal.PadOutline;
            bool[] active = player >= 0 && player < padKeyActive.Length ? padKeyActive[player] : null;
            bool triggered = active != null && key >= 0 && key < active.Length && active[key];
            // Pressed = the pad's LED colour as currently shown on the Colour tab (same as the real
            // light bar). Falls back to the per-layout default highlight (5-key amber, 4-key red
            // ←/→ / green ↑/↓) while the colour is 0/unknown or not yet read from the board.
            // Idle colours: 5-key cyan set, 4-key white set — unchanged.
            // Colour table belongs to the pad currently being edited. The other pad lights
            // with the layout default so we never paint one board's colours on the other.
            Color triggerFill = player == mDeviceIndex ? GetPadLedColor(key) : Color.Empty;
            if (triggerFill.IsEmpty)
                triggerFill = FourKeyMode ? FourKeyPressColor(kind) : FiveKeyPressDefault;
            if (triggered)
                triggerFill = Color.FromArgb(220, triggerFill);
            Color arrowTip = pal.ArrowTip, arrowMid = pal.ArrowMid, arrowBack = pal.ArrowBack, arrowGlow = pal.ArrowGlow;
            if (FourKeyMode)
                FourKeyIdleColors(out arrowTip, out arrowMid, out arrowBack, out arrowGlow);
            return new PadKeyPainter.PadKeyPaintArgs
            {
                CellBackground = cellBg,
                OutlineColor = (key == selected) ? Color.White : outline,
                SelectedFill = pal.Selected,
                SelectedGradientEnd = pal.SelectedGradientEnd,
                GlowColor = pal.Glow,
                ArrowTip = arrowTip,
                ArrowMid = arrowMid,
                ArrowBack = arrowBack,
                ArrowGlow = arrowGlow,
                BorderColor = pal.Border,
                Selected = player == mDeviceIndex && key == selected,
                Triggered = triggered,
                TriggerFill = triggerFill
            };
        }

        /// <summary>
        /// Pad highlight colour = the LED colour in ColorBuffer[key*4] (firmware Color[key*4], the
        /// byte the light bar actually uses). Color.Empty when the pad's colour is not known yet
        /// (nothing read from the board / chosen on the Colour tab since start-up or reconnect)
        /// or the code is 0 / unrecognised, so the caller can fall back to the default highlight.
        /// </summary>
        private Color GetPadLedColor(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= KEY_COUNT_5KEY || !padLedKnown[keyIndex])
                return Color.Empty;
            return LedCodeToColor(ColorBuffer[keyIndex * SENSORS_PER_KEY]);
        }

        // 5-key fallback highlight (amber, same as the selected-key colour of the light theme)
        private static readonly Color FiveKeyPressDefault = Color.FromArgb(0xF5, 0x9E, 0x0B);

        // Screen colours for the firmware LED codes R=1 G=2 B=4 Y=3 P=5 C=6 W=7
        private static Color LedCodeToColor(int led)
        {
            switch (led & 0xFF)
            {
                case LED_COLOR_R: return Color.FromArgb(0xEF, 0x44, 0x44);
                case LED_COLOR_G: return Color.FromArgb(0x22, 0xC5, 0x5E);
                case LED_COLOR_B: return Color.FromArgb(0x3B, 0x82, 0xF6);
                case LED_COLOR_Y: return Color.FromArgb(0xFA, 0xCC, 0x15);
                case LED_COLOR_P: return Color.FromArgb(0xA8, 0x55, 0xF7);
                case LED_COLOR_C: return Color.FromArgb(0x22, 0xD3, 0xEE);
                case LED_COLOR_W: return Color.FromArgb(0xFF, 0xFF, 0xFF);
                default: return Color.Empty;
            }
        }

        /// <summary>Which pads have a trustworthy LED colour in ColorBuffer: set for every pad after
        /// a successful GET_SENS(colour) and per pad when the user picks a colour on the Colour tab;
        /// cleared whenever the USB device list changes so a re-plugged board is read again.</summary>
        private readonly bool[] padLedKnown = new bool[KEY_COUNT_5KEY];

        private void MarkPadLedKnown(int keyIndex, bool known)
        {
            if (keyIndex < 0 || keyIndex >= KEY_COUNT_5KEY) return;
            padLedKnown[keyIndex] = known;
            InvalidatePadKeyButtons();
        }

        private void MarkAllPadLedKnown(bool known)
        {
            for (int k = 0; k < KEY_COUNT_5KEY; k++) padLedKnown[k] = known;
            InvalidatePadKeyButtons();
        }

        private void UpdatePadTriggerFromReport(int player, byte[] buf, int reportIndex)
        {
            if (player < 0 || player >= padKeyActive.Length)
                return;
            bool[] lamps = padKeyActive[player];
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
                if (lamps[k] != next[k])
                {
                    lamps[k] = next[k];
                    changed = true;
                }
            }
            if (changed)
                InvalidatePadKeyButtons();
        }

        private void ClearPadKeys(int player)
        {
            if (player < 0 || player >= padKeyActive.Length)
                return;
            bool[] lamps = padKeyActive[player];
            bool changed = false;
            for (int k = 0; k < lamps.Length; k++)
            {
                if (lamps[k])
                {
                    lamps[k] = false;
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
            for (int slot = 0; slot < 2; slot++)
            {
                if (!slotOnline[slot])
                {
                    ClearPadKeys(slot);
                    continue;
                }
                // Debug mode already consumed this slot's report and painted its lamps.
                if (bDebugModesFG && slot == mDeviceIndex)
                    continue;
                byte[] inputReportBuf = new byte[64];
                USBCtrl.USBLib_DeviceCtrlInput((byte)slot, DefaultOffset, reportId, reportSize, inputReportBuf);
                UpdatePadTriggerFromReport(slot, inputReportBuf, 0);
            }
        }

        private void InvalidatePadKeyButtons()
        {
            Btn_K1.Invalidate();
            Btn_K2.Invalidate();
            Btn_K3.Invalidate();
            Btn_K4.Invalidate();
            Btn_K5.Invalidate();
            if (pad2Buttons == null)
                return;
            for (int i = 0; i < pad2Buttons.Length; i++)
                if (pad2Buttons[i] != null)
                    pad2Buttons[i].Invalidate();
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
            groupBox_Button.Text = L.F("Edit.Target", PlayerName(mDeviceIndex));
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
            if (label_MapKey != null) label_MapKey.Text = L.Get(FourKeyMode ? "Map.KeyLabel4" : "Map.KeyLabel");
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
            UpdatePadCaptions();
            RefreshLanguageMenu();
        }

        private static string PlayerName(int player)
        {
            return L.Get(player == 0 ? "Radio.1P" : "Radio.2P");
        }

        private static string UsbModeName(byte mode)
        {
            byte usb = (byte)(mode & (byte)UserMode.MODE_MAK);
            if (usb == (byte)UserMode.DDR_HID) return L.Get("Radio.Gamepad");
            if (usb == (byte)UserMode.DDR_IO4) return L.Get("Radio.IO");
            return L.Get("Radio.Keyboard");
        }

        /// <summary>Caption above each pad, plus the edit-group title for the pad Read/Save will use.</summary>
        private void UpdatePadCaptions()
        {
            if (padCaptions != null)
            {
                for (int p = 0; p < padCaptions.Length; p++)
                {
                    Label lb = padCaptions[p];
                    if (lb == null) continue;
                    string name = PlayerName(p);
                    if (!slotOnline[p])
                        lb.Text = L.F("Pad.Offline", name);
                    else if (slotCache[p] != null && slotVersion[p] != null)
                        lb.Text = L.F("Pad.Online", name, UsbModeName(slotCache[p].Mode), slotVersion[p]);
                    else
                        lb.Text = L.F("Pad.OnlineNoVer", name);
                }
            }
            if (groupBox_Button != null)
            {
                groupBox_Button.Text = L.F("Edit.Target", PlayerName(mDeviceIndex));
                groupBox_Button.Invalidate();
            }
        }

        /// <summary>Clicking a key selects that pad for Read/Save and that key for the editor.</summary>
        private void SelectPadKey(int player, int key)
        {
            if (player < 0 || player > 1) return;
            if (key < 0 || key >= KeyCount) return;
            // An unplugged pad has nothing to edit. Leave Read/Save on the pad that is actually open.
            if (bDeviceStatusFG && !slotOnline[player])
                return;
            if (player != mDeviceIndex && bDebugModesFG)
                SetDeviceRawDebugMode(false);
            bool switched = player != mDeviceIndex;
            mDeviceIndex = (byte)player;
            if (switched)
            {
                // Drop the other pad's colours so the highlight does not lie.
                MarkAllPadLedKnown(false);
                if (slotCache[player] != null)
                    ApplyCachedMode(player);
                else
                {
                    ResetDeviceVersion();
                    UpdateVersionLabel();
                }
            }
            SensorIndex = key * SENSORS_PER_KEY;
            if (switched)
                PlaceEditGroup();
            UpdatePadCaptions();
            Sensor_Panel_Display_ReFlash();
        }

        private static string FormatFwVersion(int major, int minor)
        {
            if (major >= 3) return "V" + major + "." + minor;
            return major.ToString() + "." + minor.ToString("D02");
        }

        /// <summary>Push a previously read GET_MODE reply onto the editor (radios + version line).</summary>
        private void ApplyCachedMode(int player)
        {
            ModeReply reply = player >= 0 && player < slotCache.Length ? slotCache[player] : null;
            if (reply == null)
                return;
            mDevicePlayerBits = (byte)(reply.Mode & (byte)UserMode.MSEL_MAK);
            byte usbMode = (byte)(reply.Mode & (byte)UserMode.MODE_MAK);
            radioButton_KBS.Checked = usbMode == (byte)UserMode.DDR_KBS;
            radioButton_HID.Checked = usbMode == (byte)UserMode.DDR_HID;
            radioButton_IO4.Checked = usbMode == (byte)UserMode.DDR_IO4;
            mDevUid = reply.Uid;
            mDevHVer = reply.HVer;
            mFwMajor = reply.FwMajor;
            mFwMinor = reply.FwMinor;
            mFwValid = true;
            mFwFailReasonKey = "";
            UpdateVersionLabel();
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
            return Write_Config_PausingDebug(configType, configOut, len, false);
        }

        private byte Write_Config_PausingDebug(byte configType, int[] configOut, int len, bool verifyByReboot)
        {
            bool wasDebug = bDebugModesFG;
            if (wasDebug)
            {
                SetDeviceRawDebugMode(false);
                Thread.Sleep(80);
            }
            byte result = Write_Config_Value(configType, configOut, len, verifyByReboot);
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
            // Leave RAW mode, otherwise the next session cannot read anything until re-plug.
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
            // Forget the previous boards completely: version, connection state, label.
            bDeviceStatusFG = false;
            mConnState = ConnState.None;
            slotOnline[0] = slotOnline[1] = false;
            slotCache[0] = slotCache[1] = null;
            slotVersion[0] = slotVersion[1] = null;
            ClearPadKeys(0);
            ClearPadKeys(1);
            ResetDeviceVersion();
            MarkAllPadLedKnown(false);   // after a (re)connect the board's own colours win once read back
            UpdateWindowTitle();
            UpdateVersionLabel();
            UpdatePadCaptions();
            SetModeGroupEnabled(false);
            bool slot0 = false, slot1 = false;
            int pid0 = 0, pid1 = 0;
            //-------------------------------------------------------------------------------------------//V1.0.0.20:20241231(1)Display OTA Device infor on App Title
            if (USBCtrl.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                mConnState = ConnState.Ota;
                UpdateWindowTitle();
                UpdatePadCaptions();
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
                // HIDLibrary opens PID1 (1P) in slot 0 and PID2/PID3 (2P) in slot 1. Both stay open.
                // The editor follows slot 0 when it is present, otherwise slot 1. Lamps follow both.
                slotOnline[0] = slot0;
                slotOnline[1] = slot1;
                mDeviceIndex = (byte)(slot0 ? 0 : 1);
                DebugLog.Log("USB: connected VID 4D47, slot0 PID {0:X4} ({1}), slot1 PID {2:X4} ({3}); editor slot{4}",
                    pid0, slot0 ? "1P" : "absent", pid1, slot1 ? "2P" : "absent", mDeviceIndex);
                //---------------------------------------------------------------------------------------
                if (slot0) ReadDeviceMode(0, mDeviceIndex == 0);
                if (slot1) ReadDeviceMode(1, mDeviceIndex == 1);
                UpdatePadCaptions();
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
            else
            {
                DebugLog.Log("USB: no device");
                USBCtrl.USBLib_Disconnect();
                UpdatePadCaptions();
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
        /// Firmware applies debounce as DTick[key] (Tck[i/4] = DTick[i/4]), not DTick[key*4].
        /// The host mirrors that layout.
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
            // Firmware reads DTick[key]; write DTick[key*4] as well so both layouts stay in sync.
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
                if (!IsMapSlot(i)) ColorBuffer[i] = DEFAULT_COLOR_TAB[i];   // slot +1 = key map
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
            return Read_Config_Core(ConfigType, ConfigOut, Len, false);
        }

        /// <summary>
        /// GET_SENS. With <paramref name="quiet"/> nothing is shown to the user (used for the
        /// read-back check after a save); the outcome still goes to the log.
        /// </summary>
        private byte Read_Config_Core(byte ConfigType, int[] ConfigOut, int Len, bool quiet)
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
                    DebugLog.Log("GET_SENS type {0:X2}: OK crc={1:X2} values={2}", ConfigType, CS, ValuesText(ConfigOut, Len));
                    if ((ConfigType & 0x0F) == 0x01 && ConfigOut == ColorBuffer)
                        MarkAllPadLedKnown(true);   // pad highlight now follows the board's LED colours
                }
                else
                {
                    DebugLog.Log("GET_SENS type {0:X2}: CRC error calc={1:X2} got={2:X2}", ConfigType, CS, InputReportBuf[ReportIndex + PktLen - 1]);
                    if (!quiet)
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
                if (!quiet)
                    MessageBox.Show(L.F("Cfg.ReadError", why), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            if (!quiet)
                MessageBox.Show(L.Get("Cfg.ReadOk"), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
            //-------------------------------------------------------------------------------------------
        }

        private static string ValuesText(int[] values, int len)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < len && i < values.Length; i++)
            {
                if (i > 0) sb.Append(i % SENSORS_PER_KEY == 0 ? " | " : " ");
                sb.Append(values[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Read the table back right after a SET_SENS and compare it with what was sent.
        /// Returns null when every value matches, otherwise a short description of the first
        /// difference (key / sensor, sent, read). A failed read-back is reported as well.
        /// </summary>
        private string Verify_Config_Written(byte ConfigType, int[] Sent, int Len)
        {
            int[] back = new int[CONFIG_VALUE_COUNT];
            Thread.Sleep(50);
            if (Read_Config_Core(ConfigType, back, Len, true) == 0)
            {
                DebugLog.Log("VERIFY type {0:X2}: read-back failed", ConfigType);
                return L.Get("Cfg.VerifyReadFail");
            }
            for (int i = 0; i < Len; i++)
            {
                if (back[i] != Sent[i])
                {
                    DebugLog.Log("VERIFY type {0:X2}: MISMATCH at [{1}] sent={2} read={3}; sent={4}; read={5}",
                        ConfigType, i, Sent[i], back[i], ValuesText(Sent, Len), ValuesText(back, Len));
                    return L.F("Cfg.VerifyMismatch", i / SENSORS_PER_KEY + 1, (char)('A' + i % SENSORS_PER_KEY), Sent[i], back[i]);
                }
            }
            DebugLog.Log("VERIFY type {0:X2}: read-back matches", ConfigType);
            return null;
        }

        /// =============================================================================================
        /// ==== Write  Config Value ====
        /// =============================================================================================
        ///
        //CMD : 57 52 49 54 00
        //Data: 03 E8 03 E8 03 E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 03E8 
        //CS  : FA
        //RSP : 57 4F 4B FA 00
        private byte Write_Config_Value(byte ConfigType, int[] ConfigOut, int Len)
        {
            return Write_Config_Value(ConfigType, ConfigOut, Len, false);
        }

        /// <param name="verifyByReboot">
        /// After the ACK and the RAM read-back, restart the board (SET_MODE with its current
        /// mode, which the firmware answers with a reset), wait for it to enumerate again and
        /// read the table once more. Only that second read proves the values came from flash.
        /// </param>
        private byte Write_Config_Value(byte ConfigType, int[] ConfigOut, int Len, bool verifyByReboot)
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
            DebugLog.Log("SET_SENS type {0:X2}: values={1} fw={2}", ConfigType, ValuesText(ConfigOut, Len),
                mFwValid ? FwVersionText() : "?");
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
            // The ACK only says the packet was accepted. Read the table back and compare, so a
            // save that did not take effect is visible immediately instead of after a re-plug.
            string verify = Verify_Config_Written(ConfigType, ConfigOut, Len);
            if (verify != null)
            {
                MessageBox.Show(L.F("Cfg.WriteNotApplied", verify), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }
            if (!verifyByReboot)
            {
                MessageBox.Show(L.Get("Cfg.WriteOkVerified"), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
            //-------------------------------------------------------------------------------------------
            // The RAM read-back above is what the board holds right now, not what it will load at
            // the next power-up. Restart it and read the table again: that read comes from flash.
            string rebootResult = Verify_Config_AfterReboot(ConfigType, ConfigOut, Len);
            if (rebootResult != null)
            {
                // Version re-read after the restart: a known pre-V3.2 image explains a lost
                // sensitivity table by itself; anything else needs the log.
                string hint = (ConfigType & 0x0F) == 0x00 && mFwValid && !FwPersistsSensitivity()
                    ? L.F("Cfg.RebootHintOldFw", FwVersionText())
                    : L.Get("Cfg.RebootHintLog");
                MessageBox.Show(L.F("Cfg.WriteLostAfterReboot", rebootResult, hint), L.Get("Msg.SystemTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }
            MessageBox.Show(L.Get("Cfg.WriteOkRebootVerified"), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
            //-------------------------------------------------------------------------------------------
        }

        private const int REBOOT_WAIT_MS = 12000;
        private const int REBOOT_POLL_MS = 250;

        /// <summary>Mode byte the board reported last (player bits + USB mode radio buttons).</summary>
        private byte CurrentWorkMode()
        {
            byte mode = mDevicePlayerBits;
            if (radioButton_HID.Checked) mode |= (byte)UserMode.DDR_HID;
            else if (radioButton_IO4.Checked) mode |= (byte)UserMode.DDR_IO4;
            else mode |= (byte)UserMode.DDR_KBS;
            return mode;
        }

        /// <summary>
        /// SET_MODE with the current mode (the firmware stores it and resets), wait for the
        /// board to come back, read the table and compare it with what was saved.
        /// Returns null when the values survived the restart, otherwise a description.
        /// </summary>
        private string Verify_Config_AfterReboot(byte ConfigType, int[] Sent, int Len)
        {
            byte[] cmd = new byte[64];
            cmd[0] = (byte)UserCommand.SET_MODE;
            cmd[1] = CurrentWorkMode();
            DebugLog.Log("REBOOT-VERIFY type {0:X2}: TX slot{1} SET_MODE {2} to restart the board", ConfigType, mDeviceIndex, DebugLog.Hex(cmd, 0, 2));
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, 0, 64, cmd);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(400);                 // let the old handle go away before probing
            int waited = WaitForDeviceBack(REBOOT_WAIT_MS);
            if (waited < 0)
            {
                DebugLog.Log("REBOOT-VERIFY type {0:X2}: board did not come back within {1} ms (device={2}, version={3})",
                    ConfigType, REBOOT_WAIT_MS, bDeviceStatusFG, mFwValid);
                return L.Get("Cfg.RebootNoDevice");
            }
            DebugLog.Log("REBOOT-VERIFY type {0:X2}: board back after {1} ms, fw={2}", ConfigType, waited, FwVersionText());
            //-------------------------------------------------------------------------------------------
            int[] back2 = new int[CONFIG_VALUE_COUNT];
            Thread.Sleep(150);
            if (Read_Config_Core(ConfigType, back2, Len, true) == 0)
            {
                DebugLog.Log("REBOOT-VERIFY type {0:X2}: GET_SENS after restart failed", ConfigType);
                return L.Get("Cfg.RebootReadFail");
            }
            for (int i = 0; i < Len; i++)
            {
                if (back2[i] != Sent[i])
                {
                    DebugLog.Log("REBOOT-VERIFY type {0:X2}: LOST at [{1}] saved={2} after restart={3}; saved={4}; after restart={5}",
                        ConfigType, i, Sent[i], back2[i], ValuesText(Sent, Len), ValuesText(back2, Len));
                    return L.F("Cfg.RebootMismatch", i / SENSORS_PER_KEY + 1, (char)('A' + i % SENSORS_PER_KEY), Sent[i], back2[i]);
                }
            }
            DebugLog.Log("REBOOT-VERIFY type {0:X2}: values survived the restart (read from flash)", ConfigType);
            return null;
        }

        /// <summary>
        /// Drop the current handle and poll until the board enumerates again and GET_MODE
        /// succeeds. WM_DEVICECHANGE arrives on this thread, so messages are pumped while
        /// polling instead of blocking in Sleep. Returns the elapsed milliseconds, or -1 on timeout.
        /// </summary>
        private int WaitForDeviceBack(int timeoutMs)
        {
            Cursor = Cursors.WaitCursor;
            DateTime t0 = DateTime.Now;
            try
            {
                bDeviceStatusFG = false;
                USBCtrl.USBLib_Disconnect();
                while (true)
                {
                    Application.DoEvents();
                    Thread.Sleep(REBOOT_POLL_MS);
                    if (!bDeviceStatusFG) Find_USB_Deivce();
                    if (bDeviceStatusFG && mFwValid) return (int)(DateTime.Now - t0).TotalMilliseconds;
                    if ((DateTime.Now - t0).TotalMilliseconds >= timeoutMs) return -1;
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
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
                UpdatePadTriggerFromReport(mDeviceIndex, InputReportBuf, ReportIndex);
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
        private void Btn_K1_Click(object sender, EventArgs e) { SelectPadKey(0, 0); }
        private void Btn_K2_Click(object sender, EventArgs e) { SelectPadKey(0, 1); }
        private void Btn_K3_Click(object sender, EventArgs e) { SelectPadKey(0, 2); }
        private void Btn_K4_Click(object sender, EventArgs e) { SelectPadKey(0, 3); }
        private void Btn_K5_Click(object sender, EventArgs e) { SelectPadKey(0, 4); }

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
        // Writes slots +0/+2/+3 of the selected pad; slot +1 is the keymap and is never touched.
        private void SetSelectedPadColor(int ledCode)
        {
            int key = SensorIndex / 4;
            ColorBuffer[key * 4 + 0] = ledCode;
            ColorBuffer[key * 4 + 2] = ledCode;
            ColorBuffer[key * 4 + 3] = ledCode;
            MarkPadLedKnown(key, true);   // pressed highlight follows the Colour tab at once, saved or not
            Sensor_Panel_Display_ReFlash();
        }

        private void Btn_Col_R_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_R); }
        private void Btn_Col_G_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_G); }
        private void Btn_Col_B_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_B); }
        private void Btn_Col_Y_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_Y); }
        private void Btn_Col_P_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_P); }
        private void Btn_Col_C_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_C); }
        private void Btn_Col_W_Click(object sender, EventArgs e) { SetSelectedPadColor(LED_COLOR_W); }


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
            // The Colour tab now shows the default colours for the visible pads, so highlight them too
            for (int k = 0; k < KEY_COUNT_5KEY; k++)
                if (IsVisibleKey(k)) padLedKnown[k] = true;
            InvalidatePadKeyButtons();
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
            // Every save from this button ends with a board restart and a second read, so the
            // user sees right away whether the values are really in flash (see Write_Config_Value).
            if (tabControl1.SelectedIndex == 0)
            {
                ConfigType |= 0x00;
                // Up to V3.1 the board forgets the sensitivity at the next power-up; say so
                // before writing, so nobody is surprised by the post-restart comparison.
                if (mFwValid && !FwPersistsSensitivity())
                {
                    DebugLog.Log("SET_SENS type 00: firmware {0} does not keep the sensitivity across power-up, asking before writing", FwVersionText());
                    DialogResult r = MessageBox.Show(L.F("Cfg.SensiOldFwConfirm", FwVersionText()), L.Get("App.Name"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    DebugLog.Log("SET_SENS type 00: user chose {0}", r);
                    if (r != DialogResult.OK) return;
                }
                result = Write_Config_PausingDebug(ConfigType, SensiBuffer, CONFIG_VALUE_COUNT, true);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 1)
            {
                ConfigType |= 0x01;
                result = Write_Config_PausingDebug(ConfigType, ColorBuffer, CONFIG_VALUE_COUNT, true);
            }
            //-------------------------------------------------------------------------------------------
            else if (tabControl1.SelectedIndex == 2)
            {
                ConfigType |= 0x02;
                result = Write_Config_PausingDebug(ConfigType, DTickBuffer, CONFIG_VALUE_COUNT, true);
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
            // Firmware upgrade dialog; it switches the board into OTA mode itself.
            bool hadVersion = mFwValid;
            int hverBefore = mDevHVer, majorBefore = mFwMajor, minorBefore = mFwMinor;
            App_OTA ota = new App_OTA(mFwValid, mDevHVer, mFwMajor, mFwMinor);
            ota.ShowDialog(this);
            FirmwareImageInfo flashed = ota.FlashedImage;
            if (flashed == null)
            {
                // Nothing was written (cancelled / failed); just refresh the device state.
                Find_USB_Deivce();
                return;
            }
            Verify_Firmware_AfterOta(flashed, hadVersion, hverBefore, majorBefore, minorBefore);
        }

        private const int OTA_RETURN_WAIT_MS = 15000;

        /// <summary>
        /// The bootloader has no read command, so the only end-to-end proof that an upgrade took
        /// is the version the new APP reports. Wait for the board to enumerate as the APP again,
        /// read GET_MODE and compare HVer / FVer with the constants in the file that was flashed.
        /// </summary>
        private void Verify_Firmware_AfterOta(FirmwareImageInfo flashed, bool hadVersion, int hverBefore, int majorBefore, int minorBefore)
        {
            string before = hadVersion ? string.Format("HVer={0:X2} FVer={1}", hverBefore, FirmwareImageInfo.VersionText(majorBefore, minorBefore)) : "unknown";
            DebugLog.Log("OTA-VERIFY: waiting for the APP to enumerate; file {0}; board before OTA {1}", flashed.VersionLogText(), before);
            int waited = WaitForDeviceBack(OTA_RETURN_WAIT_MS);
            if (waited < 0)
            {
                DebugLog.Log("OTA-VERIFY: NO DEVICE within {0} ms (device={1}, version={2})", OTA_RETURN_WAIT_MS, bDeviceStatusFG, mFwValid);
                MessageBox.Show(L.F("Ota.VerifyNoDevice", OTA_RETURN_WAIT_MS / 1000), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string now = string.Format("HVer={0:X2} FVer={1}", mDevHVer, FwVersionText());
            if (!flashed.HasVersion)
            {
                DebugLog.Log("OTA-VERIFY: file had no version bytes; board now {0} (was {1}), back after {2} ms", now, before, waited);
                return;
            }
            if (flashed.SameVersionAs(mDevHVer, mFwMajor, mFwMinor))
            {
                bool unchanged = hadVersion && hverBefore == mDevHVer && majorBefore == mFwMajor && minorBefore == mFwMinor;
                DebugLog.Log("OTA-VERIFY: OK board reports {0} as in the file{1}, back after {2} ms",
                    now, unchanged ? " (same as before the upgrade, so a non-write cannot be told apart)" : "", waited);
                MessageBox.Show(L.F("Ota.VerifyOk", FwVersionText()), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DebugLog.Log("OTA-VERIFY: MISMATCH file {0}; board now {1}; before {2}; back after {3} ms",
                flashed.VersionLogText(), now, before, waited);
            MessageBox.Show(
                L.F("Ota.VerifyMismatch", flashed.VersionText(), FwVersionText()),
                L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            UpdatePadCaptions();
        }

        /// <summary>GET_MODE on the pad the editor is using, then refresh both captions.</summary>
        private bool ReadDeviceMode()
        {
            bool ok = ReadDeviceMode(mDeviceIndex, true);
            UpdatePadCaptions();
            return ok;
        }

        /// <summary>
        /// GET_MODE (0x47) on one USB slot. Reply (64-byte input report, Report-ID byte already stripped):
        ///   [0]=0x47  [1]=mode  [2]=UID 0xA2  [3]=HVer (0x13 5-key / 0x14 4-key)  [4]=FW major  [5]=FW minor
        /// The firmware writes this reply into its shared reply buffer (AppUser+0x11E). While the
        /// board is in RAW debug mode that buffer is overwritten with "AMRAW" frames on every scan,
        /// and the board stays in RAW mode until NOLMD or re-plug. On a RAW frame NOLMD is sent and
        /// GET_MODE is re-sent; the failure reason is kept for the label and every byte is logged.
        /// applyUi is set only for the pad the editor is following, so reading the other pad
        /// does not overwrite the version line or the Keyboard/Gamepad radios.
        /// </summary>
        private bool ReadDeviceMode(byte slot, bool applyUi)
        {
            byte saved = mDeviceIndex;
            mDeviceIndex = slot;
            try
            {
                return ReadDeviceModeOnCurrentSlot(slot, applyUi);
            }
            finally
            {
                mDeviceIndex = saved;
            }
        }

        private bool ReadDeviceModeOnCurrentSlot(byte slot, bool applyUi)
        {
            //-------------------------------------------------------------------------------------------
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            if (applyUi)
            {
                ResetDeviceVersion();
                UpdateVersionLabel();
            }
            if (!bDeviceStatusFG || slot > 1 || !slotOnline[slot])
            {
                DebugLog.Log("GET_MODE slot{0}: skipped, no device", slot);
                return false;
            }
            //-------------------------------------------------------------------------------------------
            // Only the edited pad's debug session is paused. The other pad is left alone.
            bool wasDebug = applyUi && bDebugModesFG;
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
                        // Board is still streaming RAW frames: stop it first.
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
            slotCache[slot] = reply;
            slotVersion[slot] = reply == null ? null : FormatFwVersion(reply.FwMajor, reply.FwMinor);
            if (reply == null)
            {
                if (applyUi)
                {
                    switch (last)
                    {
                        case ReplyKind.NoData: mFwFailReasonKey = "Ver.Fail.HidRead"; break;
                        case ReplyKind.Empty: mFwFailReasonKey = "Ver.Fail.NoReply"; break;
                        case ReplyKind.RawStream: mFwFailReasonKey = "Ver.Fail.RawMode"; break;
                        default: mFwFailReasonKey = "Ver.Fail.BadFrame"; break;
                    }
                    DebugLog.Log("GET_MODE slot{0}: FAILED after {1} attempts, last={2}, reason={3}", slot, GET_MODE_RETRIES, last, mFwFailReasonKey);
                    UpdateVersionLabel();   // shows the reason instead of a stale value
                }
                else
                {
                    DebugLog.Log("GET_MODE slot{0}: FAILED after {1} attempts, last={2}", slot, GET_MODE_RETRIES, last);
                }
                return false;
            }
            //-------------------------------------------------------------------------------------------
            byte mode = reply.Mode;
            string ver = slotVersion[slot];
            DebugLog.Log("GET_MODE slot{0}: OK mode=0x{1:X2} UID={2:X2}{3} HVer={4:X2}{5} FVer={6}{7}",
                slot, mode, reply.Uid, reply.UidMismatch ? "(!)" : "", reply.HVer, reply.HVerUnknown ? "(!)" : "",
                ver, sawRaw ? " (recovered from RAW mode)" : "");
            if (!applyUi)
                return true;
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
