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
    public partial class App_YJI : Form
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
        private bool bDebugModesFG = false;
  
       	private byte mDeviceIndex = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportOutID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportInID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte DefaultOffset = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue
        ///-------------------------------------------------------------------------------------------
        private string AppName = "YJI Config";

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
            try
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
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

                    KeyMpBuffer[KeyPadIndex] = kbh.vkCode;
                    App_UI_KeyPad_Display_KeyCode();
                    /*if ((kbh.vkCode == (int)KEYS_ENTER))
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
                    }*/
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
                //Console.WriteLine("USB Msg : " + m.Msg);
                //Console.WriteLine("USB WParam : " + m.WParam);
                //Console.WriteLine("USB LParam : " + m.LParam);
                //Console.WriteLine("USB HWnd : " + m.HWnd);

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
                    //((int)m.WParam == DBT_DEVICEREMOVECOMPLETE) ||
                    //((int)m.WParam == DBT_CONFIGCHANGED) ||
                    ((int)m.WParam == DBT_DEVNODES_CHANGED))
                {
                    Find_USB_Deivce();
                }
            }
            base.WndProc(ref m);
        }



        /*
        [DllImport("user32.dll")]
        public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, UInt32 Flags);

        [DllImport("user32.dll")]
        public static extern UInt32 UnregisterDeviceNotification(IntPtr hHandle);

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_DEVNODE
        {
            public int dbcd_size;
            public int dbcd_devicetype;
            public int dbcd_reserved;
            public int dbcd_devnode;
        }

        // 窗体过程函数
        [DllImport("user32.dll", EntryPoint = "DefWindowProc")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
 
        */


        //protected IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        //{
        //    Console.WriteLine("USB Msg : " + wParam);
        //    return IntPtr.Zero;
        //}

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
                string text = Value.ToString();

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
        /// ====  YJI Init  ====
        /// =============================================================================================
        ///
        public App_YJI()
        {
            ///------------------------------------------------------------------------------------------
            InitializeComponent();
            ///------------------------------------------------------------------------------------------
            ReadAppConfig();
            ///------------------------------------------------------------------------------------------
            ///
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            Find_USB_Deivce();
            ///------------------------------------------------------------------------------------------
            timer1.Enabled = true;
            timer1.Interval = 50;//ReadTagTime;
            ///------------------------------------------------------------------------------------------
            Load_Default_Sens();
            Load_Default_Mapping();
            App_UI_ReFlash();
            ///------------------------------------------------------------------------------------------
            //groupBox_Button.Visible = true;
            //groupBox_Sensi.Enabled = false;
            //groupBox_Color.Enabled = false;
            //groupBox_DTick.Enabled = false;
            ///------------------------------------------------------------------------------------------
            //tabControl1.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            progressBarLeft.Direction = ProgressBarDirection.Vertical;
            progressBarRight.Direction = ProgressBarDirection.Vertical;
            progressBarStick.Direction = ProgressBarDirection.Horizontal;
            //progressBar4.Direction = ProgressBarDirection.Vertical;




            ///------------------------------------------------------------------------------------------
            // 创建一个窗体句柄
            /*
            IntPtr windowHandle = new IntPtr(7777); // 假设的句柄值

            // 创建设备通知过滤器
            DEV_BROADCAST_DEVNODE devType = new DEV_BROADCAST_DEVNODE();
            devType.dbcd_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVNODE));
            devType.dbcd_devicetype = DBT_DEVNODES_CHANGED;
            devType.dbcd_reserved = 0;
            devType.dbcd_devnode = 0;

            IntPtr filter = Marshal.AllocHGlobal(devType.dbcd_size);
            Marshal.StructureToPtr(devType, filter, true);

            // 注册设备消息
            IntPtr handle = RegisterDeviceNotification(windowHandle, filter, 0);

            // 注册消息循环，处理设备消息
            // ...

            // 注销设备消息
            //UnregisterDeviceNotification(handle);

            // 清理
            //Marshal.FreeHGlobal(filter);
            */
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Timer Tick ====
        /// =============================================================================================
        /// 
        private void timer1_Tick(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            if (bDebugModesFG == true)
            {
                Seneor_Raw_Data_Read_and_Display();
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
            AppName = "App Config";
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
                                if (StrTab[1] == "EN")
                                {
                                    bLanguageEN = true;
                                    btn_default.Text = "DefaultSetting";
                                    btn_Read.Text = "ReadSetting";
                                    btn_Save.Text = "SaveSetting";
                                    Btn_ADJ.Text = "DebugMode";
                                    Btn_ReadMode.Text = "ReadMode";
                                    Btn_SaveMode.Text = "SaveMode";

                                    //Btn_DefaultMap.Text = "DefaultMapping";
                                    //Btn_MappingRead.Text = "ReadMapping";
                                    //Btn_MappingSave.Text = "SaveMapping";
                                    Btn_MappingMofy.Text = "ChangeMapping";
                                    Btn_ColorMofy.Text = "LEDColor";
                                }
                                else
                                {
                                    bLanguageEN = false;
                                    btn_default.Text = "默认设置";
                                    btn_Read.Text = "读取设置";
                                    btn_Save.Text = "保存设置";
                                    Btn_ADJ.Text = "调试模式";
                                    Btn_ReadMode.Text = "读取模式";
                                    Btn_SaveMode.Text = "保存模式";

                                    //Btn_DefaultMap.Text = "默认键值";
                                    //Btn_MappingRead.Text = "读取键值";
                                    //Btn_MappingSave.Text = "保存键值";
                                    Btn_MappingMofy.Text = "修改键值";
                                    Btn_ColorMofy.Text = "灯光颜色";
                                }
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
        private void App_Form_Load(object sender, EventArgs e)
        {
            Find_USB_Deivce();
        }

        /// =============================================================================================
        /// ==== Form Closing  ====
        /// =============================================================================================
        ///
        private void App_Form_FormClosing(object sender, FormClosingEventArgs e)
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
            switch (mHIDLibraryState.DeviceType)  
            {
                case DeviceType.YJI_KBS_PID: break;
                case DeviceType.YJI_HID_PID: break;
                case DeviceType.YJI_IO4_PID: break;
                default: break;
            }
        }

        /// =============================================================================================
        /// ==== Find USB Connected ====
        /// =============================================================================================
        ///
        private void Find_USB_Deivce()
        {
            //-------------------------------------------------------------------------------------------
            USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //-------------------------------------------------------------------------------------------
            this.Text = AppName;
            label_Ver.Text = "";
            groupBox_Mode.Enabled = false;
            groupBox_Sensi.Enabled = false;
            bDeviceStatusFG = false;
            //-------------------------------------------------------------------------------------------
            if (USBCtrl.USBLib_Connect((int)DeviceType.YJI_KBS_VID, (int)DeviceType.YJI_KBS_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                bDeviceStatusFG = true;
              	mDeviceIndex = 0;
                ReportOutID = 0xA2;
                ReportInID = 0x01;
                DefaultOffset = 0x01;
            }
            //-------------------------------------------------------------------------------------------
            else if (USBCtrl.USBLib_Connect((int)DeviceType.YJI_HID_VID, (int)DeviceType.YJI_HID_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                bDeviceStatusFG = true;
              	mDeviceIndex = 0;
                ReportOutID = 0x01;
                ReportInID = 0x01;
                DefaultOffset = 0x01;
            }
          	//-------------------------------------------------------------------------------------------
            else if (USBCtrl.USBLib_Connect((int)DeviceType.YJI_IO4_VID, (int)DeviceType.YJI_IO4_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                bDeviceStatusFG = true;
              	mDeviceIndex = 0;
                ReportOutID = 0x10;
                ReportInID = 0x01;
                DefaultOffset = 0x00; 
            }
            //-------------------------------------------------------------------------------------------//V1.0.0.20:20241231(1)Display OTA Device infor on App Title
            else if (USBCtrl.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None) != (int)DeviceType.None)
            {
                this.Text = AppName + " ( OTA Device Connected)";
                return;
            }

            //-------------------------------------------------------------------------------------------
            if (bDeviceStatusFG == true)
            {
                //---------------------------------------------------------------------------------------
                this.Text = AppName + " (Device Connected)";
                groupBox_Mode.Enabled = true;
                groupBox_Sensi.Enabled = true;
                //---------------------------------------------------------------------------------------
                Read_WorkMode();
                //---------------------------------------------------------------------------------------
            }
            //-------------------------------------------------------------------------------------------
            else
            {
                USBCtrl.USBLib_Disconnect();
            }
            //-------------------------------------------------------------------------------------------
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  Read Work Mode   ====
        /// =============================================================================================
        private void Read_WorkMode()
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
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
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_MODE)
            {
                if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.APP_KBS)
                {
                    radioButton_KBS.Checked = true;
                    radioButton_HID.Checked = false;
                    radioButton_IO4.Checked = false;
                }
                else if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.APP_HID)
                {
                    radioButton_KBS.Checked = false;
                    radioButton_HID.Checked = true;
                    radioButton_IO4.Checked = false;
                }
                else if ((InputReportBuf[ReportIndex + 1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.APP_IO4)
                {
                    radioButton_KBS.Checked = false;
                    radioButton_HID.Checked = false;
                    radioButton_IO4.Checked = true;
                }

                //V1.0.0.15:20240920(1)Change ADX TP Delay Config
                if (InputReportBuf[ReportIndex + 2] == (byte)UserProID.ID_YJI)
                {
                    label_Ver.Text = "UID=" + InputReportBuf[ReportIndex + 2].ToString("X02") + ", " +
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

        private void Write_WorkMode()
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte index = 0;
            //byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //byte Txd_CS = 0;
            //-------------------------------------------------------------------------------------------
            byte WorkMode = 0;
            WorkMode = (byte)UserMode.APP_1PS;
            if (radioButton_KBS.Checked == true)
            {
                WorkMode |= (byte)UserMode.APP_KBS;
            }
            else if (radioButton_HID.Checked == true)
            {
                WorkMode |= (byte)UserMode.APP_HID;
            }
            else if (radioButton_IO4.Checked == true)
            {
                WorkMode |= (byte)UserMode.APP_IO4;
            }
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_MODE);
            SendByteBuf[index++] = (byte)(WorkMode);
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            Read_WorkMode();
            //-------------------------------------------------------------------------------------------
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
            //ConfigType:   Bit[7:4] : Reserved
            //              Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick,  0x03=KeyMapping  
            SendByteBuf[index++] = (byte)(ConfigType);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
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
                byte DataLen = 16 * 2;
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = USBCtrl.UPDATE_CRC(ReportIndex, InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[ReportIndex + PktLen - 1])
                {
                    //byte WMCUCount = 4 * 4;     //WMCU   : Chan1-4
                    //byte ExChip1Count = 4 * 3;  //ExChip1: Chan5-7
                    //byte ExChip2Count = 4 * 2;  //ExChip2: Chan8-9
                    byte ConfigOutIndex = 0;
                    //if ((ConfigType & 0xF0) == 0x00) ConfigOutIndex = 0;
                    //else if ((ConfigType & 0xF0) == 0x10) ConfigOutIndex = (byte)(WMCUCount);
                    //else if ((ConfigType & 0xF0) == 0x20) ConfigOutIndex = (byte)(WMCUCount + ExChip1Count);

                    for (int i = 0; i < Len; i++)
                    {
                        ConfigOut[ConfigOutIndex + i] = ((InputReportBuf[ReportIndex + SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[ReportIndex + SyncLen + (i * 2 + 1)]));
                    }

                    //for (int i = 0; i < Len; i++)
                    //{
                    //    ConfigOut[i] = ((InputReportBuf[ReportIndex + SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[ReportIndex + SyncLen + (i * 2 + 1)]));
                    //}
                }
                else
                {
                    MessageBox.Show("Read Config CS Error !!! (" + CS.ToString("X02") + "," + InputReportBuf[ReportIndex + PktLen - 1].ToString("X02") + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    + InputReportBuf[ReportIndex + 4].ToString("X02") + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            //-------------------------------------------------------------------------------------------
            //MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        private byte Write_Config_Value(byte ConfigType, int[] ConfigOut, int Len)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
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
            //ConfigType:   Bit[7:4] : Reserved
            //              Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick,  0x03=KeyMapping  
            SendByteBuf[index++] = (byte)(ConfigType);
            //-------------------------------------------------------------------------------------------
            //byte WMCUCount = 4 * 4;     //WMCU   : Chan1-4
            //byte ExChip1Count = 4 * 3;  //ExChip1: Chan5-7
            //byte ExChip2Count = 4 * 2;  //ExChip2: Chan8-9
            byte ConfigOutIndex = 0;
            //if ((ConfigType & 0xF0) == 0x00) ConfigOutIndex = 0;
            //else if ((ConfigType & 0xF0) == 0x10) ConfigOutIndex = (byte)(WMCUCount);
            //else if ((ConfigType & 0xF0) == 0x20) ConfigOutIndex = (byte)(WMCUCount + ExChip1Count);
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < Len; i++)
            {
                SendByteBuf[index++] = (byte)((ConfigOut[ConfigOutIndex + i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((ConfigOut[ConfigOutIndex + i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            Txd_CS = USBCtrl.UPDATE_CRC(0, SendByteBuf, index);
            SendByteBuf[index++] = Txd_CS;
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B &&
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
            //MessageBox.Show("Write Config Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            byte ReportIndex = 0x00;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            UInt16[] RawBuffer = new UInt16[64];
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);//V1.0.0.18:20241012(1)Change ADX Save Issue
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
                progressBarLeft.Value = RawBuffer[SenIndex + 0];
                progressBarRight.Value = RawBuffer[SenIndex + 1];
                progressBarStick.Value = RawBuffer[SenIndex + 2];
                //progressBar4.Value = RawBuffer[SenIndex + 3];
                //---------------------------------------------------------------------------------------
            }
        }

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

        private const int MAX_BTN_NUM = 16;
        private const int MAX_CONFIG_CHAN = 16;
        private const int DEFAULT_SENSI = 800;
        private const int MAX_VAL_SENSI = 4095;
        private const int DEFAULT_COLOR = LED_COLOR_W;
        private const int DEFAULT_COLOR1 = LED_COLOR_B;
        private const int DEFAULT_COLOR2 = LED_COLOR_R;
        private const int DEFAULT_COLOR3 = LED_COLOR_B;
        private const int DEFAULT_COLOR4 = LED_COLOR_R;

        private const int DEFAULT_DTICK = 0x03;

        

        private bool bMappingModifFG = false;
        private bool bColorModifFG = false;
        private int KeyPadIndex = 0;
        
        //private int[] ConfigBuffer = new int[64];

        private int SensorIndex = 1;
        private int[] SensiBuffer = new int[MAX_CONFIG_CHAN];
        private int[] ColorBuffer = new int[MAX_CONFIG_CHAN];
        private int[] DTickBuffer = new int[MAX_CONFIG_CHAN];
        private int[] KeyMpBuffer = new int[MAX_CONFIG_CHAN];

        private int HallBtnIndex_L = 10;
        private int HallBtnIndex_R = 11;

        readonly int[] DEFAULT_SENSI_TAB = new int[]//9*4
        {
          DEFAULT_SENSI, // 0 : 
          DEFAULT_SENSI, // 1 : 
          DEFAULT_SENSI, // 2 : 
          DEFAULT_SENSI, // 3 : 
  
          DEFAULT_SENSI, // 4 : 
          DEFAULT_SENSI, // 5 : 
          DEFAULT_SENSI, // 6 : 
          DEFAULT_SENSI, // 7 : 
  
          DEFAULT_SENSI, // 8 : 
          DEFAULT_SENSI, // 9 : 
          DEFAULT_SENSI, //10 : 
          DEFAULT_SENSI, //11 : 
  
          DEFAULT_SENSI, //12 : 
          DEFAULT_SENSI, //13 : 
          DEFAULT_SENSI, //14 : 
          DEFAULT_SENSI, //15 : 
        };

        readonly int[] DEFAULT_COLOR_TAB = new int[]//9*4
        {
          DEFAULT_COLOR, // 0 : 
          DEFAULT_COLOR, // 1 : 
          DEFAULT_COLOR, // 2 : 
          DEFAULT_COLOR, // 3 : 
  
          DEFAULT_COLOR, // 4 : 
          DEFAULT_COLOR, // 5 : 
          DEFAULT_COLOR, // 6 : 
          DEFAULT_COLOR, // 7 : 
  
          DEFAULT_COLOR, // 8 : 
          DEFAULT_COLOR, // 9 : 
          DEFAULT_COLOR, //10 : 
          DEFAULT_COLOR, //11 : 
  
          DEFAULT_COLOR, //12 : 
          DEFAULT_COLOR, //13 : 
          DEFAULT_COLOR, //14 : 
          DEFAULT_COLOR, //15 : 
        };
        readonly int[] DEFAULT_DTICK_TAB = new int[]//9*4
        {
          DEFAULT_DTICK, // 0 : 
          DEFAULT_DTICK, // 1 : 
          DEFAULT_DTICK, // 2 : 
          DEFAULT_DTICK, // 3 : 
  
          DEFAULT_DTICK, // 4 : 
          DEFAULT_DTICK, // 5 : 
          DEFAULT_DTICK, // 6 : 
          DEFAULT_DTICK, // 7 : 
  
          DEFAULT_DTICK, // 8 : 
          DEFAULT_DTICK, // 9 : 
          DEFAULT_DTICK, //10 : 
          DEFAULT_DTICK, //11 : 
  
          DEFAULT_DTICK, //12 : 
          DEFAULT_DTICK, //13 : 
          DEFAULT_DTICK, //14 : 
          DEFAULT_DTICK, //15 : 
        };

        readonly int[] KeyCodeMappingDefault = new int[]//32+6
        {
          //0x53,0x44,0x46,   //'s','d','f' 
          //0x4a,0x4b,0x4c,   //'j','k','l'
          //0x20,0x0d,        //'Space','Enter'
          //0x70,0x71,        //'F1','F2'
          //0xA0,0x50,        //'shift','p'
          //0x25,0x27,        //'Left','Right'    //AnalogStick

          0x53,0x44,0x46,0x4a,0x4b,0x4c,    //主功能键1-6   : s,d,f, j,k,l
          0x57,0x49,                        //菜单键1-2     : w, i
          //0x61,0x62,                      //功能侧键1-2   : Num1, Num2
          0x31,0x32,                        //功能侧键1-2   : 1, 2
          0x41,0xba,                        //左右霍尔键1-2 : a, ;
          0x00,0x00,0x00,0x00,  
        };

        readonly string[] vkCodeStringMapping = new string[]//32+6
        {
            //0x00-0x07
            " "," "," "," "," "," "," "," ",   
            //0x08-0x0F
            "BackSpace","Tab"," "," ","Clear","Enter"," "," ",   

            //0x10-0x17
            "","","","Pause","CapLock"," "," "," ",
            //0x18-0x1f
            "","","","Esc","","","","",

            //0x20-0x27
            "Space","PageUp","PageDown","End","Home","Left","Up","Right", 
            //0x28-0x2F
            "Down","Select","Print","Execute","PrtSc","Insert","Delete","Help",

            //0x30-0x37
            "0","1","2","3","4","5","6","7", 
            //0x38-0x3F
            "8","9","","","","","","", 

            //0x40-0x47
            "","A","B","C","D","E","F","G", 
            //0x48-0x4F
            "H","I","J","K","L","M","N","O", 

            //0x50-0x57
            "P","Q","R","S","T","U","V","W", 
            //0x58-0x5F
            "X","Y","Z","Win-L","Win-R","Paste","","", 

            //0x60-0x67
            "Num0","Num1","Num2","Num3","Num4","Num5","Num6","Num7", 
            //0x68-0x6F
            "Num8","Num9","Num*","Num+","Sepa","Num-","Num.","Num/", 

            //0x70-0x77
            "F1","F2","F3","F4","F5","F6","F7","F8", 
            //0x78-0x7F
            "F9","F10","F11","F12","","","","", 


            //0x80-0x87
            "","","","","","","","", 
            //0x88-0x8F
            "","","","","","","","", 

            //0x90-0x97
            "NumLock","ScrLock","","","","","","", 
            //0x98-0x9F
            "","","","","","","","", 

            //0xA0-0xA7
            "Shift-L","Shift-R","Ctrl-L","Ctrl-R","Alt-L","Alt-R","","", 
            //0xA8-0xAF
            "","","","","","","","",

            //0xB0-0xB7
            "","","","","","","","", 
            //0xB8-0xBF
            "","",";","=",",","-",".","/",

            //0xC0-0xC7
            "`","","","","","","","", 
            //0xC8-0xCF
            "","","","","","","","",

            //0xD0-0xD7
            "","","","","","","","", 
            //0xD8-0xDF
            "","","","[","|","]","'","",

            //0xE0-0xE7
            "","","","","","","","", 
            //0xE8-0xEF
            "","","","","","","","",

            //0xF0-0xF7
            "","","","","","","","", 
            //0xF8-0xFF
            "","","","","","","","Fn",
        };

        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        private void Load_Default_Sens()
        {
            for (int i = 0; i < MAX_CONFIG_CHAN; i++)
            {
                SensiBuffer[i] = DEFAULT_SENSI_TAB[i];
                ColorBuffer[i] = DEFAULT_COLOR_TAB[i];
                DTickBuffer[i] = DEFAULT_DTICK_TAB[i];
            }
        }

        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        //private void Load_Default_Setting()
        //{
        //    for (int i = 0; i < MAX_TOUCH_NUM; i++)
        //    {
        //        TP_SenBuffer[i] = TP_SenDefault[i];
        //    }
        //}
        private void Load_Default_Mapping()
        {
            for (int i = 0; i < (MAX_BTN_NUM); i++)
            {
                KeyMpBuffer[i] = KeyCodeMappingDefault[i];
            }
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  Btn Read Mode  Click  ====
        /// =============================================================================================
        private void Btn_ReadMode_Click(object sender, EventArgs e)
        {
            Read_WorkMode();
        }

        private void Btn_SaveMode_Click(object sender, EventArgs e)
        {
            Write_WorkMode();
        }

        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        private void btn_default_Click(object sender, EventArgs e)
        {
            Load_Default_Sens();
            Load_Default_Mapping();
            App_UI_ReFlash();
        }

        /// =============================================================================================
        /// ====  Btn Read Click  ====
        /// =============================================================================================
        ///
        private void btn_Read_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            string RxStr = "";
            byte result = 0x00;
            byte ConfigType = 0x00; //Bit[3:0] : 0x00=Sensi, 0x01=Color,  0x02=DTick,  0x03=KeyMapping  
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x00;
            result = Read_Config_Value(ConfigType, SensiBuffer, MAX_CONFIG_CHAN); RxStr = "Sensi:"; for (int i = 0; i < MAX_CONFIG_CHAN; i++) { RxStr += SensiBuffer[i].ToString("X02"); } Console.WriteLine(RxStr);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x01;
            result = Read_Config_Value(ConfigType, ColorBuffer, MAX_CONFIG_CHAN); RxStr = "Color:"; for (int i = 0; i < MAX_CONFIG_CHAN; i++) { RxStr += ColorBuffer[i].ToString("X02"); } Console.WriteLine(RxStr);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x02;
            result = Read_Config_Value(ConfigType, DTickBuffer, MAX_CONFIG_CHAN); RxStr = "DTick:"; for (int i = 0; i < MAX_CONFIG_CHAN; i++) { RxStr += DTickBuffer[i].ToString("X02"); } Console.WriteLine(RxStr);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x03;
            result = Read_Config_Value(ConfigType, KeyMpBuffer, MAX_CONFIG_CHAN); RxStr = "KeyMp:"; for (int i = 0; i < MAX_CONFIG_CHAN; i++) { RxStr += KeyMpBuffer[i].ToString("X02"); } Console.WriteLine(RxStr);
            //-------------------------------------------------------------------------------------------
            if (result != 0)
            {
                MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                App_UI_ReFlash();
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
            byte result = 0x00;
            byte ConfigType = 0x00;
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x00;
            result = Write_Config_Value(ConfigType, SensiBuffer, MAX_CONFIG_CHAN);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x01;
            result = Write_Config_Value(ConfigType, ColorBuffer, MAX_CONFIG_CHAN);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x02;
            result = Write_Config_Value(ConfigType, DTickBuffer, MAX_CONFIG_CHAN);
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            ConfigType = 0x03;
            result = Write_Config_Value(ConfigType, KeyMpBuffer, MAX_CONFIG_CHAN);
            //-------------------------------------------------------------------------------------------
            if (result != 0)
            {
                MessageBox.Show("Write Config Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                App_UI_ReFlash();
            }
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Btn ADJ Debug Click  ====
        /// =============================================================================================
        ///
        private void Btn_ADJ_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            if (bDebugModesFG == false)
            {
                SendByteBuf[index++] = (byte)UserCommand.SET_MRAW;
                SendByteBuf[index++] = (byte)(0x52);    //R
                SendByteBuf[index++] = (byte)(0x41);    //A
                SendByteBuf[index++] = (byte)(0x57);    //W
                SendByteBuf[index++] = (byte)(0x4D);    //M
                SendByteBuf[index++] = (byte)(0x44);    //D
                USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);

                bDebugModesFG = true;
                if (bLanguageEN == true)
                {
                    Btn_ADJ.Text = "Stop";
                }
                else
                {
                    Btn_ADJ.Text = "停止";
                }

                groupBox_Mode.Enabled = false;
                //btn_default.Enabled = false;
                //btn_Read.Enabled = false;
                //btn_Save.Enabled = false;
                Btn_MappingMofy.Enabled = false;

            }
            else
            {
                SendByteBuf[index++] = (byte)UserCommand.SET_MRAW;
                SendByteBuf[index++] = (byte)(0x4E);    //N
                SendByteBuf[index++] = (byte)(0x4F);    //O
                SendByteBuf[index++] = (byte)(0x4C);    //L
                SendByteBuf[index++] = (byte)(0x4D);    //M
                SendByteBuf[index++] = (byte)(0x44);    //D
                USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);

                bDebugModesFG = false;
                if (bLanguageEN == true)
                {
                    Btn_ADJ.Text = "DebugMode";
                }
                else
                {
                    Btn_ADJ.Text = "调试模式";
                }

                groupBox_Mode.Enabled = true;
                //btn_default.Enabled = true;
                //btn_Read.Enabled = true;
                //btn_Save.Enabled = true;
                Btn_MappingMofy.Enabled = true;
            }
        }

        /// =============================================================================================
        /// ====  Btn OTA Click  ====
        /// =============================================================================================
        ///
        private void Btn_OTA_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            DialogResult result;
            result = MessageBox.Show("Enter OTA Mode !   " + "\r\n" + "Are you sure to Set Device OTA Mode ? ", "Warning Message", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.No)
            {
                return;
            }
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            SendByteBuf[0] = (byte)UserCommand.SET_OTA;
            SendByteBuf[1] = (byte)'O';
            SendByteBuf[2] = (byte)'T';
            SendByteBuf[3] = (byte)'A';
            //-------------------------------------------------------------------------------------------
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            //App_OTA ota = new App_OTA();
            //this.Hide();
            //ota.Show();
            //-------------------------------------------------------------------------------------------
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== UI Keypad Display ====
        /// =============================================================================================
        private void App_UI_KeyPad_Display_IndexNum()
        {
            Btn_K1.Text = "1";
            Btn_K2.Text = "2";
            Btn_K3.Text = "3";
            Btn_K4.Text = "4";
            Btn_K5.Text = "5";
            Btn_K6.Text = "6";
            Btn_K7.Text = "7";
            Btn_K8.Text = "8";
            Btn_K9.Text = "9";
            Btn_KA.Text = "10";
            Btn_Left.Text = "L";
            Btn_Right.Text = "R";
            AnalogStick.Text = "Stick";
        }
        /// =============================================================================================
        /// ==== UI Keypad Display ====
        /// =============================================================================================
        private void App_UI_KeyPad_Display_KeyCode()
        {
            Btn_K1.Text = vkCodeStringMapping[KeyMpBuffer[0]];
            Btn_K2.Text = vkCodeStringMapping[KeyMpBuffer[1]];
            Btn_K3.Text = vkCodeStringMapping[KeyMpBuffer[2]];
            Btn_K4.Text = vkCodeStringMapping[KeyMpBuffer[3]];
            Btn_K5.Text = vkCodeStringMapping[KeyMpBuffer[4]];
            Btn_K6.Text = vkCodeStringMapping[KeyMpBuffer[5]];
            Btn_K7.Text = vkCodeStringMapping[KeyMpBuffer[6]];
            Btn_K8.Text = vkCodeStringMapping[KeyMpBuffer[7]];
            Btn_K9.Text = vkCodeStringMapping[KeyMpBuffer[8]];
            Btn_KA.Text = vkCodeStringMapping[KeyMpBuffer[9]];
            Btn_Left.Text = vkCodeStringMapping[KeyMpBuffer[10]];
            Btn_Right.Text = vkCodeStringMapping[KeyMpBuffer[11]];

            textBox_vkCode.Text = "0x" + KeyMpBuffer[KeyPadIndex].ToString("X02");
            textBox_Mapping.Text = vkCodeStringMapping[KeyMpBuffer[KeyPadIndex]];
            //textBox_Mapping.Text = KeyMappingBuffer[KeyPadIndex].ToString();
        }

        /// =============================================================================================
        /// ==== UI Keypad Display ====
        /// =============================================================================================
        private void App_UI_KeyPad_Display_HilightColor()
        {
            //------------------------------------------------------------------------------------------------------K1
            if (ColorBuffer[0] == (byte)LED_COLOR_R) { Btn_K1.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_G) { Btn_K1.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_B) { Btn_K1.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_Y) { Btn_K1.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_P) { Btn_K1.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_C) { Btn_K1.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[0] == (byte)LED_COLOR_W) { Btn_K1.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K2
            if (ColorBuffer[1] == (byte)LED_COLOR_R) { Btn_K2.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_G) { Btn_K2.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_B) { Btn_K2.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_Y) { Btn_K2.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_P) { Btn_K2.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_C) { Btn_K2.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[1] == (byte)LED_COLOR_W) { Btn_K2.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K3
            if (ColorBuffer[2] == (byte)LED_COLOR_R) { Btn_K3.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_G) { Btn_K3.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_B) { Btn_K3.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_Y) { Btn_K3.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_P) { Btn_K3.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_C) { Btn_K3.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[2] == (byte)LED_COLOR_W) { Btn_K3.BackColor = System.Drawing.Color.White; }


            //------------------------------------------------------------------------------------------------------K4
            if (ColorBuffer[3] == (byte)LED_COLOR_R) { Btn_K4.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_G) { Btn_K4.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_B) { Btn_K4.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_Y) { Btn_K4.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_P) { Btn_K4.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_C) { Btn_K4.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[3] == (byte)LED_COLOR_W) { Btn_K4.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K5
            if (ColorBuffer[4] == (byte)LED_COLOR_R) { Btn_K5.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_G) { Btn_K5.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_B) { Btn_K5.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_Y) { Btn_K5.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_P) { Btn_K5.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_C) { Btn_K5.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[4] == (byte)LED_COLOR_W) { Btn_K5.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K6
            if (ColorBuffer[5] == (byte)LED_COLOR_R) { Btn_K6.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_G) { Btn_K6.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_B) { Btn_K6.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_Y) { Btn_K6.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_P) { Btn_K6.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_C) { Btn_K6.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[5] == (byte)LED_COLOR_W) { Btn_K6.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K7
            if (ColorBuffer[6] == (byte)LED_COLOR_R) { Btn_K7.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_G) { Btn_K7.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_B) { Btn_K7.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_Y) { Btn_K7.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_P) { Btn_K7.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_C) { Btn_K7.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[6] == (byte)LED_COLOR_W) { Btn_K7.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K8
            if (ColorBuffer[7] == (byte)LED_COLOR_R) { Btn_K8.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_G) { Btn_K8.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_B) { Btn_K8.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_Y) { Btn_K8.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_P) { Btn_K8.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_C) { Btn_K8.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[7] == (byte)LED_COLOR_W) { Btn_K8.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------K9
            if (ColorBuffer[8] == (byte)LED_COLOR_R) { Btn_K9.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_G) { Btn_K9.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_B) { Btn_K9.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_Y) { Btn_K9.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_P) { Btn_K9.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_C) { Btn_K9.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[8] == (byte)LED_COLOR_W) { Btn_K9.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------KA
            if (ColorBuffer[9] == (byte)LED_COLOR_R) { Btn_KA.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_G) { Btn_KA.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_B) { Btn_KA.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_Y) { Btn_KA.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_P) { Btn_KA.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_C) { Btn_KA.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[9] == (byte)LED_COLOR_W) { Btn_KA.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------KLeft
            if (ColorBuffer[10] == (byte)LED_COLOR_R) { Btn_Left.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_G) { Btn_Left.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_B) { Btn_Left.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_Y) { Btn_Left.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_P) { Btn_Left.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_C) { Btn_Left.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[10] == (byte)LED_COLOR_W) { Btn_Left.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------KRight
            if (ColorBuffer[11] == (byte)LED_COLOR_R) { Btn_Right.BackColor = System.Drawing.Color.Red; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_G) { Btn_Right.BackColor = System.Drawing.Color.Green; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_B) { Btn_Right.BackColor = System.Drawing.Color.Blue; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_Y) { Btn_Right.BackColor = System.Drawing.Color.Yellow; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_P) { Btn_Right.BackColor = System.Drawing.Color.Fuchsia; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_C) { Btn_Right.BackColor = System.Drawing.Color.Cyan; }
            else if (ColorBuffer[11] == (byte)LED_COLOR_W) { Btn_Right.BackColor = System.Drawing.Color.White; }
            //------------------------------------------------------------------------------------------------------
        }

        private void App_UI_KeyPad_Display_NormalColor()
        {
            Btn_K1.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K2.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K3.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K4.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K5.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K6.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K7.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K8.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_K9.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_KA.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_Left.BackColor = System.Drawing.SystemColors.ButtonFace;
            Btn_Right.BackColor = System.Drawing.SystemColors.ButtonFace;
        }


        /// =============================================================================================
        /// ====  Btn Modify Key Mapping Click  ====
        /// =============================================================================================
        ///
        private void Btn_MappingMofy_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            if (bMappingModifFG == false)
            {
                bMappingModifFG = true;
                groupBox_Btn.Enabled = true;
                groupBox_Mode.Enabled = false;
                groupBox_Sensi.Enabled = true;
                textBox_vkCode.Enabled = true;
                textBox_Mapping.Enabled = true;
                label_NewKey.Enabled = true;
                Btn_ADJ.Enabled = false;
                //Read_KeyMapping_Config();
                Hook_Start();
                if (bLanguageEN == true)
                {
                    Btn_MappingMofy.Text = "Exit";
                }
                else
                {
                    Btn_MappingMofy.Text = "退出";
                }
            }
            //----------------------------------------------------------------------------
            else
            {
                bMappingModifFG = false;
                groupBox_Btn.Enabled = true;
                groupBox_Mode.Enabled = true;
                groupBox_Sensi.Enabled = true;
                textBox_vkCode.Enabled = false;
                textBox_Mapping.Enabled = false;
                label_NewKey.Enabled = false;
                Btn_ADJ.Enabled = true;

                Hook_Clear();
                if (bLanguageEN == true)
                {
                    Btn_MappingMofy.Text = "ChangeMapping";
                }
                else
                {
                    Btn_MappingMofy.Text = "键值修改";
                }
            }

            App_UI_ReFlash();
            //----------------------------------------------------------------------------
        }

        private void Btn_ColorMofy_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            if (bColorModifFG == false)
            {
                bColorModifFG = true;
                groupBox_Color.Visible = true;
                //groupBox_Btn.Enabled = true;
                //groupBox_Mode.Enabled = false;
                //groupBox_Sensi.Enabled = true;

                //textBox_vkCode.Enabled = true;
                //textBox_Mapping.Enabled = true;
                //label_NewKey.Enabled = true;
                //Btn_ADJ.Enabled = false;
                //Btn_OTA.Enabled = false;
                
                if (bLanguageEN == true)
                {
                    Btn_ColorMofy.Text = "Exit";
                }
                else
                {
                    Btn_ColorMofy.Text = "退出";
                }
            }
            //----------------------------------------------------------------------------
            else
            {
                bColorModifFG = false;
                groupBox_Color.Visible = false;

                //groupBox_Btn.Enabled = true;
                //groupBox_Mode.Enabled = true;
                //groupBox_Sensi.Enabled = true;
                //textBox_vkCode.Enabled = false;
                //textBox_Mapping.Enabled = false;
                //label_NewKey.Enabled = false;
                //Btn_ADJ.Enabled = true;
                //Btn_OTA.Enabled = true;

                Hook_Clear();
                if (bLanguageEN == true)
                {
                    Btn_ColorMofy.Text = "LEDColor";
                }
                else
                {
                    Btn_ColorMofy.Text = "灯光颜色";
                }
            }

            App_UI_ReFlash();
            //----------------------------------------------------------------------------
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== UI ReFlash ====
        /// =============================================================================================
        private void App_UI_ReFlash()
        {
            //----------------------------------------------------------------------------
            if (bMappingModifFG == true)
            {
                App_UI_KeyPad_Display_KeyCode();
            }
            else
            {
                App_UI_KeyPad_Display_IndexNum();
            }
            //----------------------------------------------------------------------------
            if (bColorModifFG == true)
            {
                App_UI_KeyPad_Display_HilightColor();
            }
            else
            {
                App_UI_KeyPad_Display_NormalColor();
            }
            //----------------------------------------------------------------------------
            //App_UI_KeyPad_Btn_BackColor();
            //----------------------------------------------------------------------------
            int SenIndex = 0;
            int SensVal = 0;
            //----------------------------------------------------------------------------
            SensVal = SensiBuffer[SenIndex + HallBtnIndex_L];
            textBoxLeft.Text = SensVal.ToString();
            trackBar_Left.Value = SensVal;

            SensVal = SensiBuffer[SenIndex + HallBtnIndex_R];
            textBoxRight.Text = SensVal.ToString();
            trackBar_Rgiht.Value = SensVal;
            //----------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Btn 1-12 Click ====
        /// =============================================================================================
        private void Btn_K1_Click_1(object sender, EventArgs e)
        {
            KeyPadIndex = 0;
            App_UI_ReFlash();
        }

        private void Btn_K2_Click_1(object sender, EventArgs e)
        {
            KeyPadIndex = 1;
            App_UI_ReFlash();
        }

        private void Btn_K3_Click_1(object sender, EventArgs e)
        {
            KeyPadIndex = 2;
            App_UI_ReFlash();
        }

        private void Btn_K4_Click_1(object sender, EventArgs e)
        {
            KeyPadIndex = 3;
            App_UI_ReFlash();
        }

        private void Btn_K5_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 4;
            App_UI_ReFlash();
        }

        private void Btn_K6_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 5;
            App_UI_ReFlash();
        }

        private void Btn_K7_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 6;
            App_UI_ReFlash();
        }

        private void Btn_K8_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 7;
            App_UI_ReFlash();
        }

        private void Btn_K9_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 8;
            App_UI_ReFlash();
        }

        private void Btn_KA_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 9;
            App_UI_ReFlash();
        }

        private void Btn_Left_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 10;
            App_UI_ReFlash();
        }

        private void Btn_Right_Click(object sender, EventArgs e)
        {
            KeyPadIndex = 11;
            App_UI_ReFlash();
        }


        private void Btn_Col_R_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_R;
            App_UI_ReFlash();
        }

        private void Btn_Col_G_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_G;
            App_UI_ReFlash();
        }

        private void Btn_Col_B_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_B;
            App_UI_ReFlash();
        }

        private void Btn_Col_Y_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_Y;
            App_UI_ReFlash();
        }

        private void Btn_Col_P_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_P;
            App_UI_ReFlash();
        }

        private void Btn_Col_C_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_C;
            App_UI_ReFlash();
        }

        private void Btn_Col_W_Click(object sender, EventArgs e)
        {
            ColorBuffer[KeyPadIndex] = LED_COLOR_W;
            App_UI_ReFlash();
        }

        /// =============================================================================================
        /// ==== track Bar Changed====
        /// =============================================================================================
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar_Left.Value;
            int SenIndex = 0;// (SensorIndex / 4) * 4;
            textBoxLeft.Text = SensVal.ToString();
            SensiBuffer[SenIndex + HallBtnIndex_L] = SensVal;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar_Rgiht.Value;
            int SenIndex = 0;// (SensorIndex / 4) * 4;
            textBoxRight.Text = SensVal.ToString();
            SensiBuffer[SenIndex + HallBtnIndex_R] = SensVal;
        }

        /// =============================================================================================
        /// ==== Text Box Changed ====
        /// =============================================================================================
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBoxLeft.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = 0;// (SensorIndex / 4) * 4;
                    textBoxLeft.Text = SensVal.ToString();
                    trackBar_Left.Value = SensVal;
                    SensiBuffer[SenIndex + HallBtnIndex_L] = SensVal;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string InputStr = textBoxRight.Text;
                if (InputStr.Length > 0)
                {
                    int SensVal = Convert.ToInt32(InputStr);
                    int SenIndex = 0;// (SensorIndex / 4) * 4;
                    textBoxRight.Text = SensVal.ToString();
                    trackBar_Rgiht.Value = SensVal;
                    SensiBuffer[SenIndex + HallBtnIndex_R] = SensVal;
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









    }
}
