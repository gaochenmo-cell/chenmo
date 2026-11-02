//////////////////////////////////////////////////////////////////////////////////
//	MainForm.cs
//	Managed PS3 Library Tester
//////////////////////////////////////////////////////////////////////////////////

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using System.Threading;
using System.Runtime.InteropServices;
//using Microsoft.Office.Interop.Excel;
using HIDLibraryNS;


namespace ConfigAppNS
{
	public partial class App_ADX : Form
	{

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        ///-------------------------------------------------------------------------------------------
        HidLibrary USBCtrl = new HidLibrary();

        private delegate void UpdateUSBHIDStateDelegate(HIDLibraryLibChangedEventArgs args);

        ///-------------------------------------------------------------------------------------------
        private bool bDeviceStatusFG = false;
        private bool bTouchTestMode = false;
        private bool bButtonTestMode = false;
        private bool bLanguageEN = false;
        ///-------------------------------------------------------------------------------------------
        private string AppName = "Config App";

        ///-------------------------------------------------------------------------------------------
        System.IO.FileInfo[] files;

        ///-------------------------------------------------------------------------------------------
        [DllImport("winmm.dll")]
        public static extern bool PlaySound(String Filename, int Mod, int Flags);

        ///-------------------------------------------------------------------------------------------
        byte[] TouchDispBuf = new byte[10];
        byte[] TouchDispReg = new byte[10];
				private byte mDeviceIndex = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportOutID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportInID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte DefaultOffset = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue
        ///-------------------------------------------------------------------------------------------
        //public const int USBD_MSEL_MAK = 0xF0;
        //public const int USBD_MODE_1PS = 0x00;
        //public const int USBD_MODE_2PS = 0x80;
        //public const int USBD_MODE_MAK = 0x0F;
        //public const int USBD_MODE_KBS = 0x00;
        //public const int USBD_MODE_HID = 0x01;
        //public const int USBD_MODE_IO4 = 0x02;

        //public const int USBD_USER_CMD_GET_MODE = 0x47;
        //public const int USBD_USER_CMD_SET_MODE = 0x4D;

        //public const int USBD_USER_CMD_GET_SENS = 0x52;        
        //public const int USBD_USER_CMD_SET_SENS = 0x57;

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
            //bool bStatus = false;
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
                	//V1.0.0.18:20241012(1)Change ADX Save Issue
                	Find_USB_Deivce();
                }
            }
            base.WndProc(ref m);
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ====  ADX Init  ====
        /// =============================================================================================
        ///
		public App_ADX()
		{
            ///------------------------------------------------------------------------------------------
			InitializeComponent();
            ///------------------------------------------------------------------------------------------
            ReadAppConfig();
            ///------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //bDeviceStatusFG = false;
            //if (USBCtrl.Connect() == true) bDeviceStatusFG = true;
            //DeivceFirstConnected();

            Find_USB_Deivce();
            ///------------------------------------------------------------------------------------------
            timer1.Enabled  = true;
            timer1.Interval = 100;//ReadTagTime;

            ///------------------------------------------------------------------------------------------
            // Populate the ListView.Items property.
            // Set the directory to the sample picture directory.
            string Imagepath = Directory.GetCurrentDirectory() + "\\Image";
            System.IO.DirectoryInfo dirInfo = new System.IO.DirectoryInfo(Imagepath);
            // Get the .jpg files from the directory
            files = dirInfo.GetFiles("*.png");
            ///------------------------------------------------------------------------------------------
            if (files == null)
            {
                MessageBox.Show("Read Image Config Error", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ///------------------------------------------------------------------------------------------
            PictureBox_Disable_all();
            Load_Default_Sens();
            pictureBox.Visible = true;
            panel1.Visible = false;
            panel2.Visible = false;
            groupBox_Edit.Visible = true;
            trackBar_Val.Enabled = false;
            label_Pkt_Count.Text = "";
            //groupBox_Index.Text = "";
            label_TPValue.Text = "";
            label_Val.Text = "";

            button1.Visible = false;
            button2.Visible = false;
            Btn_Default_S1.Visible = false;
            Btn_Default_S2.Visible = false;
            Btn_Default_S3.Visible = false;
            Btn_Default_S4.Visible = false;
            ///------------------------------------------------------------------------------------------
            //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
            checkBox_All.Visible = false;
            checkBox_A.Checked = false; //V1.0.0.26:20250412(2)ADX All/A/B/C/D/E check disable
            checkBox_B.Checked = false;
            checkBox_C.Checked = false;
            checkBox_D.Checked = false;
            checkBox_E.Checked = false;

            btn_TouchTest.Enabled = false;
            btn_ButtonTest.Enabled = false;
            btn_default.Enabled = false;
            btn_Read.Enabled = true;
            btn_Save.Enabled = false;
            Btn_Delay_Config.Enabled = false;

            Btn_ReadMode.Enabled = false;
            Btn_WriteMode.Enabled = false;

            Btn_Sens_Open.Enabled = false;
            Btn_Sens_Save.Enabled = false;

            checkBox_A.Enabled = false;
            checkBox_B.Enabled = false;
            checkBox_C.Enabled = false;
            checkBox_D.Enabled = false;
            checkBox_E.Enabled = false;

            //V1.0.0.26:20250412(1)ADX Seni Select
            groupBox_Sensi.Visible = false;
            ///------------------------------------------------------------------------------------------
            //V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
            Btn_RGB.Enabled = false;
            groupBox_RGB.Visible = false;
            trackBar_RGB.Value = 255;
            label_RGB.Text = "Brightness=100.0%(255)";
            Brightness = 255;
            ColorRGB_R = 255;
            ColorRGB_G = 255;
            ColorRGB_B = 255;
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====  Form Load ====
        /// =============================================================================================
        ///
		private void ADX_Form_Load(object sender, EventArgs e)
		{
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //bDeviceStatusFG = false;
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //if (USBCtrl.Connect() == true) bDeviceStatusFG = true;
            //DeivceFirstConnected(); 
            Find_USB_Deivce();   
		}

        /// =============================================================================================
        /// ==== Form Closing  ====
        /// =============================================================================================
        ///
        private void ADX_Form_FormClosing(object sender, FormClosingEventArgs e)
		{
            USBCtrl.USBLib_Disconnect();
            if (MyCom.SerialStatus == true)  //V1.0.0.30:20250510(1)ADX COM Port Release
            {
                Close_Serial_Port();
            }
		}

        /// =============================================================================================
        /// ==== HID Event Changed  ====
        /// =============================================================================================
        ///
        private void HID_Event_Changed_Delegate(object sender, HIDLibraryLibChangedEventArgs args)
		{
            //BeginInvoke(new UpdatePS3ControllerStateDelegate(UpdateHidLibraryState), args);
            BeginInvoke(new UpdateUSBHIDStateDelegate(UpdateHidLibraryState), args);
		}


        /// =============================================================================================
        /// ==== Find USB Connected ====
        /// =============================================================================================
        ///
        private void Find_USB_Deivce()	//V1.0.0.18:20241012(1)Change ADX Save Issue
        {
            //-------------------------------------------------------------------------------------------
            USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //-------------------------------------------------------------------------------------------
            this.Text = AppName;
            label_Ver.Text = "";
            groupBox_Mode.Enabled = false;
            radioBtn1P.Enabled = false;
            radioBtn2P.Enabled = false;
            radioBtn1P.Checked = false;
            radioBtn2P.Checked = false;
            bDeviceStatusFG = false;
            //-------------------------------------------------------------------------------------------
            if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_KBS_VID1, (int)DeviceType.ADX_KBS_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn1P.Enabled = true;
                radioBtn1P.Checked = true;
                mDeviceIndex = 0;
                ReportOutID = 0x00;
                ReportInID = 0x00;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_HID_VID1, (int)DeviceType.ADX_HID_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn1P.Enabled = true;
                radioBtn1P.Checked = true;
                mDeviceIndex = 0;
                ReportOutID = 0x01;
                ReportInID = 0x01;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_IO4_VID1, (int)DeviceType.ADX_IO4_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn1P.Enabled = true;
                radioBtn1P.Checked = true;
                mDeviceIndex = 0;
                ReportOutID = 0x10;
                ReportInID = 0x01;
                DefaultOffset = 0x00;
            }
            //-------------------------------------------------------------------------------------------
            if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_KBS_VID2, (int)DeviceType.None, (int)DeviceType.ADX_KBS_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn2P.Enabled = true;
                radioBtn2P.Checked = true;
                mDeviceIndex = 1;
                ReportOutID = 0x00;
                ReportInID = 0x00;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_HID_VID2, (int)DeviceType.None, (int)DeviceType.ADX_HID_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn2P.Enabled = true;
                radioBtn2P.Checked = true;
                mDeviceIndex = 1;
                ReportOutID = 0x01;
                ReportInID = 0x01;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ADX_IO4_VID2, (int)DeviceType.None, (int)DeviceType.ADX_IO4_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                radioBtn2P.Enabled = true;
                radioBtn2P.Checked = true;
                mDeviceIndex = 1;
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
                //---------------------------------------------------------------------------------------
                Btn_ReadMode_Click(null, null);
                //---------------------------------------------------------------------------------------
            }
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
        /// ==== Display All ====
        /// =============================================================================================
        ///
        UInt16 LastSliderLP1, LastSliderRP1, LastDgtBtn1P1, LastDgtBtn2P1;
        UInt16 ColinP1 = 0;

        UInt16 LastSliderLP2, LastSliderRP2, LastDgtBtn1P2, LastDgtBtn2P2;
        UInt16 ColinP2 = 0;

        private void UpdateHidLibraryDisplay(int Index, UInt16 SliderL, UInt16 SliderR, UInt16 DgtBtn1, UInt16 DgtBtn2, UInt16 Colin)
        {
            ///------------------------------------------------------------------------------------------
            if ((DgtBtn1 & 0x8000) != 0) { pictureBox_BTN4.Visible = true; }
            else { pictureBox_BTN4.Visible = false; }
            if ((DgtBtn1 & 0x0001) != 0) { pictureBox_BTN3.Visible = true; }
            else { pictureBox_BTN3.Visible = false; }
            if ((DgtBtn1 & 0x0008) != 0) { pictureBox_BTN2.Visible = true; }
            else { pictureBox_BTN2.Visible = false; }
            if ((DgtBtn1 & 0x0004) != 0) { pictureBox_BTN1.Visible = true; }
            else { pictureBox_BTN1.Visible = false; }

            if ((DgtBtn1 & 0x0800) != 0) { pictureBox_BTN8.Visible = true; }
            else { pictureBox_BTN8.Visible = false; }
            if ((DgtBtn1 & 0x1000) != 0) { pictureBox_BTN7.Visible = true; }
            else { pictureBox_BTN7.Visible = false; }
            if ((DgtBtn1 & 0x2000) != 0) { pictureBox_BTN6.Visible = true; }
            else { pictureBox_BTN6.Visible = false; }
            if ((DgtBtn1 & 0x4000) != 0) { pictureBox_BTN5.Visible = true; }
            else { pictureBox_BTN5.Visible = false; }

            if ((DgtBtn1 & 0x0002) != 0) { pictureBox_BTN9.Visible = true; }
            else { pictureBox_BTN9.Visible = false; }
            if ((DgtBtn1 & 0x0200) != 0) { pictureBox_BTN10.Visible = true; }
            else { pictureBox_BTN10.Visible = false; }
            if ((DgtBtn1 & 0x0040) != 0) { pictureBox_BTN11.Visible = true; }
            else { pictureBox_BTN11.Visible = false; }
            ///------------------------------------------------------------------------------------------
            string RxStr = "IO4 : " + SliderL.ToString("X04") + "," + SliderR.ToString("X04") + "," + DgtBtn1.ToString("X04") + "," + DgtBtn2.ToString("X04") + "," + ColinP1.ToString();

            if (Index == (int)(DeviceType.ADX_IO4_PID1))
            {
                RxStr += "(" + DeviceType.ADX_IO4_PID1 + ")";
            }
            else
            {
                RxStr += "(" + DeviceType.ADX_IO4_PID2 + ")";
            }
            Console.WriteLine(RxStr);
            ///------------------------------------------------------------------------------------------
        }

        private void UpdateHidLibraryState(HIDLibraryLibChangedEventArgs args)
		{
            HIDLibraryState mHIDLibraryState = args.HIDLibraryState;
            switch (mHIDLibraryState.DeviceType)
            {
            		//V1.0.0.18:20241012(1)Change ADX Save Issue   
                ///------------------------------------------------------------------------------------------
                case DeviceType.ADX_IO4_PID1:
                    if (mHIDLibraryState.IO4.DgtBtn1 != 00)
                    {
                    }

                    if (LastSliderLP1 !=mHIDLibraryState.IO4.SliderL ||
                        LastSliderRP1 != mHIDLibraryState.IO4.SliderR ||
                        LastDgtBtn1P1 != mHIDLibraryState.IO4.DgtBtn1 ||
                        LastDgtBtn2P1 != mHIDLibraryState.IO4.DgtBtn2)
                    {
                        LastSliderLP1 = mHIDLibraryState.IO4.SliderL;
                        LastSliderRP1 = mHIDLibraryState.IO4.SliderR;
                        LastDgtBtn1P1 = mHIDLibraryState.IO4.DgtBtn1;
                        LastDgtBtn2P1 = mHIDLibraryState.IO4.DgtBtn2;

                        if ((LastSliderLP1 & 0x0f00) == 0x0100)
                        {
                            ColinP1++;
                        }

                        UpdateHidLibraryDisplay((int)mHIDLibraryState.DeviceType, LastSliderLP1, LastSliderRP1, LastDgtBtn1P1, LastDgtBtn2P1, ColinP1); 
                    }

                    break;
                ///------------------------------------------------------------------------------------------
                case DeviceType.ADX_IO4_PID2:
                    if (mHIDLibraryState.IO4.DgtBtn1 != 00)
                    {
                    }

                    if (LastSliderLP2 != mHIDLibraryState.IO4.SliderL ||
                        LastSliderRP2 != mHIDLibraryState.IO4.SliderR ||
                        LastDgtBtn1P2 != mHIDLibraryState.IO4.DgtBtn1 ||
                        LastDgtBtn2P2 != mHIDLibraryState.IO4.DgtBtn2)
                    {
                        LastSliderLP2 = mHIDLibraryState.IO4.SliderL;
                        LastSliderRP2 = mHIDLibraryState.IO4.SliderR;
                        LastDgtBtn1P2 = mHIDLibraryState.IO4.DgtBtn1;
                        LastDgtBtn2P2 = mHIDLibraryState.IO4.DgtBtn2;

                        if ((LastSliderLP2 & 0x0f00) == 0x0100)
                        {
                            ColinP2++;
                        }

                        UpdateHidLibraryDisplay((int)mHIDLibraryState.DeviceType, LastSliderLP2, LastSliderRP2, LastDgtBtn1P2, LastDgtBtn2P2, ColinP2); 
                    }
  
                    //Console.Write("IO4 :" + mHIDLibraryState.IO4.DgtBtn1);
                    break;
                    ///------------------------------------------------------------------------------------------
                default:
                    break;
            }
		}

        /// =============================================================================================
        /// ==== Timer Tick ====
        /// =============================================================================================
        /// 
        private void timer2_Tick(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            bool NewFlag = false;
            for (int i = 0; i < 10; i++)
            {
                if (TouchDispReg[i] != TouchDispBuf[i])
                {
                    TouchDispReg[i] = TouchDispBuf[i];
                    NewFlag = true;
                }
            }
            ///------------------------------------------------------------------------------------------
            if (NewFlag)
            {
                ///--------------------------------------------------------------------------------------
                PictureBox_Disable_all();
                ///--------------------------------------------------------------------------------------
                if ((TouchDispReg[1] & 0x01) != 0) { pictureBox_A1.Visible = true; }	//A1
                else { pictureBox_A1.Visible = false; }	
                if ((TouchDispReg[1] & 0x02) != 0) { pictureBox_A2.Visible = true; }	//A2
                else { pictureBox_A2.Visible = false; }	
                if ((TouchDispReg[1] & 0x04) != 0) { pictureBox_A3.Visible = true; }	//A3
                else { pictureBox_A3.Visible = false; }	
                if ((TouchDispReg[1] & 0x08) != 0) { pictureBox_A4.Visible = true; }	//A4
                else { pictureBox_A4.Visible = false; }	
                if ((TouchDispReg[1] & 0x10) != 0) { pictureBox_A5.Visible = true; }	//A5
                else { pictureBox_A5.Visible = false; }	

                if ((TouchDispReg[2] & 0x01) != 0) { pictureBox_A6.Visible = true; }	//A6
                else { pictureBox_A6.Visible = false; }	
                if ((TouchDispReg[2] & 0x02) != 0) { pictureBox_A7.Visible = true; }	//A7
                else { pictureBox_A7.Visible = false; }	
                if ((TouchDispReg[2] & 0x04) != 0) { pictureBox_A8.Visible = true; }	//A8
                else { pictureBox_A8.Visible = false; }	
                if ((TouchDispReg[2] & 0x08) != 0) { pictureBox_B1.Visible = true; }	//B1
                else { pictureBox_B1.Visible = false; }	
                if ((TouchDispReg[2] & 0x10) != 0) { pictureBox_B2.Visible = true; }	//B2
                else { pictureBox_B2.Visible = false; }

                if ((TouchDispReg[3] & 0x01) != 0) { pictureBox_B3.Visible = true; }	//B3
                else { pictureBox_B3.Visible = false; }
                if ((TouchDispReg[3] & 0x02) != 0) { pictureBox_B4.Visible = true; }	//B4
                else { pictureBox_B4.Visible = false; }
                if ((TouchDispReg[3] & 0x04) != 0) { pictureBox_B5.Visible = true; }	//B5
                else { pictureBox_B5.Visible = false; }
                if ((TouchDispReg[3] & 0x08) != 0) { pictureBox_B6.Visible = true; }	//B6
                else { pictureBox_B6.Visible = false; }
                if ((TouchDispReg[3] & 0x10) != 0) { pictureBox_B7.Visible = true; }	//B7
                else { pictureBox_B7.Visible = false; }

                if ((TouchDispReg[4] & 0x01) != 0) { pictureBox_B8.Visible = true; }	//B8
                else { pictureBox_B8.Visible = false; }
                if ((TouchDispReg[4] & 0x02) != 0) { pictureBox_C1.Visible = true; }	//C1 
                else { pictureBox_C1.Visible = false; }
                if ((TouchDispReg[4] & 0x04) != 0) { pictureBox_C2.Visible = true; }	//C2
                else { pictureBox_C2.Visible = false; }
                if ((TouchDispReg[4] & 0x08) != 0) { pictureBox_D1.Visible = true; }	//D1
                else { pictureBox_D1.Visible = false; }
                if ((TouchDispReg[4] & 0x10) != 0) { pictureBox_D2.Visible = true; }	//D2
                else { pictureBox_D2.Visible = false; }

                if ((TouchDispReg[5] & 0x01) != 0) { pictureBox_D3.Visible = true; }	//D3
                else { pictureBox_D3.Visible = false; }
                if ((TouchDispReg[5] & 0x02) != 0) { pictureBox_D4.Visible = true; }	//D4
                else { pictureBox_D4.Visible = false; }
                if ((TouchDispReg[5] & 0x04) != 0) { pictureBox_D5.Visible = true; }	//D5
                else { pictureBox_D5.Visible = false; }
                if ((TouchDispReg[5] & 0x08) != 0) { pictureBox_D6.Visible = true; }	//D6
                else { pictureBox_D6.Visible = false; }
                if ((TouchDispReg[5] & 0x10) != 0) { pictureBox_D7.Visible = true; }	//D7
                else { pictureBox_D7.Visible = false; }

                if ((TouchDispReg[6] & 0x01) != 0) { pictureBox_D8.Visible = true; }	//D8
                else { pictureBox_D8.Visible = false; }
                if ((TouchDispReg[6] & 0x02) != 0) { pictureBox_E1.Visible = true; }	//E1
                else { pictureBox_E1.Visible = false; }
                if ((TouchDispReg[6] & 0x04) != 0) { pictureBox_E2.Visible = true; }	//E2
                else { pictureBox_E2.Visible = false; }
                if ((TouchDispReg[6] & 0x08) != 0) { pictureBox_E3.Visible = true; }	//E3
                else { pictureBox_E3.Visible = false; }
                if ((TouchDispReg[6] & 0x10) != 0) { pictureBox_E4.Visible = true; }	//E4
                else { pictureBox_E4.Visible = false; }

                if ((TouchDispReg[7] & 0x01) != 0) { pictureBox_E5.Visible = true; }	//E5
                else { pictureBox_E5.Visible = false; }
                if ((TouchDispReg[7] & 0x02) != 0) { pictureBox_E6.Visible = true; }	//E6
                else { pictureBox_E6.Visible = false; }
                if ((TouchDispReg[7] & 0x04) != 0) { pictureBox_E7.Visible = true; }	//E7
                else { pictureBox_E7.Visible = false; }
                if ((TouchDispReg[7] & 0x08) != 0) { pictureBox_E8.Visible = true; }	//E8
                else { pictureBox_E8.Visible = false; }
                ///--------------------------------------------------------------------------------------
                //PictureBox_Update();
                ///--------------------------------------------------------------------------------------
            }
            ///------------------------------------------------------------------------------------------
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            //Point mousePosition = Control.MousePosition;
            Point mousePosition = System.Windows.Forms.Cursor.Position;
            int LabHeight = (this.Height - this.ClientRectangle.Height) + groupBox_Edit.Height;
            int Y = (mousePosition.Y - LabHeight);
            //if(Y<0)Y=0;
            //Logtxtbx.Text = string.Format("X:{0}  Y:{1}", mousePosition.X, Y);
            label_Pkt_Count.Text = string.Format("X:{0}  Y:{1}", mousePosition.X, Y);
            ///------------------------------------------------------------------------------------------
            if(ReadConfigTO>0)
            {
                ReadConfigTO--;
                if (ReadConfigTO == 0)
                {
                    MessageBox.Show("Read Config Timeout!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            if(WriteConfigTO>0)
            {
                WriteConfigTO--;
                if (WriteConfigTO == 0)
                {
                    MessageBox.Show("Write Config Timeout!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            ///------------------------------------------------------------------------------------------
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        SerialCom MyCom = new SerialCom();
        private Thread _mSerial_ServerThread;

        String COMPort = "COM3";
        String BaudRate = "115200-8-N";

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
            AppName = "ConfigApp";
            //string ComText = "COM9";
            //string BaudText = "115200-N-8-1";
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
                            else if (Str == "COM")
                            {
                                COMPort = StrTab[1];
                            }
                            else if (Str == "BAUDRATE")
                            {
                                BaudRate = StrTab[1];
                            }
                            else if (Str == "LANGUAGE")
                            {
                                if (StrTab[1] == "EN")
                                {
                                    bLanguageEN = true;
                                    btn_TouchTest.Text = "TouchTest";
                                    btn_ButtonTest.Text = "ButtonTest";
                                    btn_default.Text = "DefaultSetting";
                                    btn_Read.Text = "ReadSetting";
                                    btn_Save.Text = "SaveSetting";
                                    Btn_Delay_Config.Text = "TP Delay";

                                    Btn_ReadMode.Text = "Read Mode";
                                    Btn_WriteMode.Text = "Save Mode";

                                    Btn_Sens_Open.Text = "Import Sensi";//V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI//V1.0.0.23:20250322:(2)ADX Sens Save as and Import
                                    Btn_Sens_Save.Text = "Sensi Save as";

                                    //V1.0.0.26:20250412(1)ADX Seni Select
                                    BtnSensi1.Text = "Very low";
                                    BtnSensi2.Text = "Low";
                                    BtnSensi3.Text = "Medium";
                                    BtnSensi4.Text = "High";
                                    BtnSensi5.Text = "Very High";

                                    /// V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
                                    Btn_RGB_Sel.Text = "Sel Color";
                                    Btn_RGB_Read.Text = "Read";
                                    Btn_RGB_Save.Text = "Save";
                                }
                                else
                                {
                                    bLanguageEN = false;
                                    btn_TouchTest.Text = "触摸屏测试";
                                    btn_ButtonTest.Text = "按键测试";
                                    btn_default.Text = "默认设置";
                                    btn_Read.Text = "读取设置";
                                    btn_Save.Text = "保存设置";
                                    Btn_Delay_Config.Text = "触屏延时";

                                    Btn_ReadMode.Text = "读取模式";//V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
                                    Btn_WriteMode.Text = "保存模式";

                                    Btn_Sens_Open.Text = "灵敏度导入";//V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI //V1.0.0.23:20250322:(2)ADX Sens Save as and Import
                                    Btn_Sens_Save.Text = "灵敏度另存";

                                    //V1.0.0.26:20250412(1)ADX Seni Select
                                    BtnSensi1.Text = "很低";
                                    BtnSensi2.Text = "较低";
                                    BtnSensi3.Text = "中等";
                                    BtnSensi4.Text = "较高";
                                    BtnSensi5.Text = "很高";

                                    /// V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
                                    Btn_RGB_Sel.Text = "选择颜色";
                                    Btn_RGB_Read.Text = "读取";
                                    Btn_RGB_Save.Text = "保存";
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
        /// ==== Clear Display TextBox ====
        /// =============================================================================================
        /// 
        private void btnClear_Click(object sender, EventArgs e)
        {
            //Logtxtbx.Text = "";
            //Logtxtbx.SelectionStart = Logtxtbx.Text.Length;
            //Logtxtbx.ScrollToCaret();
        }
        /// =============================================================================================
        /// ==== Set Status TextBox ====
        /// =============================================================================================
        ///
        /*delegate void SetStatusTextBoxCallback(String NewStr);
        private void SetStatusTextBox(String NewStr)
        {
            if (this.Logtxtbx.InvokeRequired)
            {
                SetStatusTextBoxCallback d = new SetStatusTextBoxCallback(SetStatusTextBox);
                this.Invoke(d, new object[] { NewStr });
            }
            else
            {
                this.Logtxtbx.Text += NewStr + "\r\n";
                this.Logtxtbx.SelectionStart = this.Logtxtbx.Text.Length;
                this.Logtxtbx.ScrollToCaret();
            }
        }*/
        /// =============================================================================================
        /// ==== Open Serial Port ====
        /// =============================================================================================
        ///
        void Open_Serial_Port()
        {
            if (MyCom.SerialStatus == false)
            {
                try
                {
                    //SetStatusTextBox("Open COM Port ...");
                    if (MyCom.OpenSerialPort(COMPort, BaudRate))
                    //if (MyCom.OpenSerialPort("COM3", "115200-8-N"))
                    {
                        this._mSerial_ServerThread = new Thread(Serial_Port_Thread);     // 创建监听线程          
                        this._mSerial_ServerThread.Start();                              // 开启线程
                        //SetStatusTextBox("Open COM Port Succeed");
                    }
                    else
                    {
                        //SetStatusTextBox("Open COM Port Failed");
                    }
                }
                catch (Exception ex)
                {
                    Console.Write("Open Serial Port Error:" + ex.Message);
                }
            }
        }

        /// =============================================================================================
        /// ==== Close Serial Port ====
        /// =============================================================================================
        ///
        void Close_Serial_Port()
        {
            if (MyCom.SerialStatus == true)
            {
                MyCom.CloseSerialPort();
                this._mSerial_ServerThread.Abort();
                //SetStatusTextBox("Close COM Port Succeed");
            }
        }

        /// =============================================================================================
        /// ==== Serial Port Thread ====
        /// =============================================================================================
        ///
        void Serial_Port_Thread()
        {
            string RxStr = "";
            byte[] TouchBuf = new byte[10];
            byte[] TouchReg = new byte[10];
            ///------------------------------------------------------------------------------------------
            try
            {
                bool bWaitSerialFlag = true;
                while (bWaitSerialFlag)
                {
                    //---------------------------------------------------------------------------------- New Rx Start
                    if (MyCom.bNewRxFlag == true)
                    {
                        //-------------------------------------------------------------------------------
                        if (MyCom.RxFlag1 == true)
                        {
                            MyCom.RxLength = MyCom.RxLen1; //RxStr = "R1:" + MyCom.RxLength.ToString("X02") + ",";
                            MyCom.RxLen1   = 0;
                            for (int i = 0; i < MyCom.RxLength; i++)
                            {
                                MyCom.Serialbuffer[i] = MyCom.Serialbuffer1[i]; //RxStr += MyCom.Serialbuffer[i].ToString("X02");
                            }
                            //Console.WriteLine(RxStr);

                            if (MyCom.RxLength == 9 && 
                                MyCom.Serialbuffer[0] == 0x28 &&    //SyncStart
                                MyCom.Serialbuffer[8] == 0x29)      //SyncEnd
                            {
                                TouchDispBuf[0] = TouchBuf[0] = MyCom.Serialbuffer[0];
                                TouchDispBuf[1] = TouchBuf[1] = MyCom.Serialbuffer[1];
                                TouchDispBuf[2] = TouchBuf[2] = MyCom.Serialbuffer[2];
                                TouchDispBuf[3] = TouchBuf[3] = MyCom.Serialbuffer[3];
                                TouchDispBuf[4] = TouchBuf[4] = MyCom.Serialbuffer[4];
                                TouchDispBuf[5] = TouchBuf[5] = MyCom.Serialbuffer[5];
                                TouchDispBuf[6] = TouchBuf[6] = MyCom.Serialbuffer[6];
                                TouchDispBuf[7] = TouchBuf[7] = MyCom.Serialbuffer[7];
                                TouchDispBuf[8] = TouchBuf[8] = MyCom.Serialbuffer[8];
                            }

                            MyCom.RxFlag1 = false;
                        }
                        //-------------------------------------------------------------------------------
                        else if (MyCom.RxFlag2 == true)
                        {
                            MyCom.RxLength = MyCom.RxLen2; RxStr = "R2:" + MyCom.RxLength.ToString("X02") + ",";
                            MyCom.RxLen2 = 0;
                            for (int i = 0; i < MyCom.RxLength; i++)
                            {
                                MyCom.Serialbuffer[i] = MyCom.Serialbuffer2[i]; RxStr += MyCom.Serialbuffer[i].ToString("X02");
                            }
                            Console.WriteLine(RxStr); 
                            MyCom.RxFlag2 = false;
                        }
                        //-------------------------------------------------------------------------------
                        else if (MyCom.RxFlag3 == true)
                        {
                            MyCom.RxLength = MyCom.RxLen3; RxStr = "R3:" + MyCom.RxLength.ToString("X02") + ",";
                            MyCom.RxLen3 = 0;
                            for (int i = 0; i < MyCom.RxLength; i++)
                            {
                                MyCom.Serialbuffer[i] = MyCom.Serialbuffer3[i]; RxStr += MyCom.Serialbuffer[i].ToString("X02");
                            }
                            Console.WriteLine(RxStr);
                            MyCom.RxFlag3 = false;
                        }
                        //-------------------------------------------------------------------------------
                        //string RxStr = "";
                        //for (byte i = 0; i < MyCom.RxLength; i++)
                        //{
                        //    RxStr += MyCom.Serialbuffer[i].ToString("X02");
                        //}
                        //Console.WriteLine(RxStr);
                        //-------------------------------------------------------------------------------
                        if (bTouchTestMode == true)
                        {
                        }
                        //-------------------------------------------------------------------------------
                        else if (MyCom.Serialbuffer[0] == 0xA5 && MyCom.Serialbuffer[1] == 0x5A)
                        {
                        }
                        //-------------------------------------------------------------------------------
                        MyCom.bNewRxFlag = false;
                        MyCom.RxLength = 0x00;
                        MyCom.RxdNose = 0x00;
                        //-------------------------------------------------------------------------------
                    }
                    //---------------------------------------------------------------------------------- New Rx End

                    //---------------------------------------------------------------------------------- Touch Test Mode Start
                    if (bTouchTestMode == true)
                    {
                        bool NewFlag = false;
                        for (int i = 0; i < 10; i++)
                        {
                            if (TouchReg[i] != TouchBuf[i])
                            {
                                TouchReg[i] = TouchBuf[i];
                                NewFlag = true;
                            }
                        }
                        if (NewFlag)
                        {
                            //---------------------------------------------------------------------------
                            RxStr = "RAW:";
                            for (byte i = 0; i < 10; i++)
                            {
                                RxStr += TouchReg[i].ToString("X02")+" ";
                            }
                            //Console.WriteLine(RxStr);
                            //---------------------------------------------------------------------------
                            //RxStr = "";
                            RxStr += "\r\nTP:\r\n";
                            int count = 0;
                            if ((TouchReg[1] & 0x01) != 0) { RxStr += "A1 "; count++; }	//A1
                            if ((TouchReg[1] & 0x02) != 0) { RxStr += "A2 "; count++; }	//A2
                            if ((TouchReg[1] & 0x04) != 0) { RxStr += "A3 "; count++; }	//A3
                            if ((TouchReg[1] & 0x08) != 0) { RxStr += "A4 "; count++; }	//A4
                            if ((TouchReg[1] & 0x10) != 0) { RxStr += "A5 "; count++; }	//A5
                            if ((TouchReg[2] & 0x01) != 0) { RxStr += "A6 "; count++; }	//A6
                            if ((TouchReg[2] & 0x02) != 0) { RxStr += "A7 "; count++; }	//A7
                            if ((TouchReg[2] & 0x04) != 0) { RxStr += "A8 "; count++; }	//A8
                            if (count != 0) RxStr += "\r\n";
                            count = 0;
                            if ((TouchReg[2] & 0x08) != 0) { RxStr += "B1 "; count++; }	//B1
                            if ((TouchReg[2] & 0x10) != 0) { RxStr += "B2 "; count++; }	//B2
                            if ((TouchReg[3] & 0x01) != 0) { RxStr += "B3 "; count++; }	//B3
                            if ((TouchReg[3] & 0x02) != 0) { RxStr += "B4 "; count++; }	//B4
                            if ((TouchReg[3] & 0x04) != 0) { RxStr += "B5 "; count++; }	//B5
                            if ((TouchReg[3] & 0x08) != 0) { RxStr += "B6 "; count++; }	//B6
                            if ((TouchReg[3] & 0x10) != 0) { RxStr += "B7 "; count++; }	//B7
                            if ((TouchReg[4] & 0x01) != 0) { RxStr += "B8 "; count++; }	//B8
                            if (count != 0) RxStr += "\r\n";
                            count = 0;
                            if ((TouchReg[4] & 0x02) != 0) { RxStr += "C1 "; count++; }	//C1        //V1.0.0.3:20231203(1)Touch Screen Test C1 C2 Issue
                            if ((TouchReg[4] & 0x04) != 0) { RxStr += "C2 "; count++; }	//C2
                            if (count != 0) RxStr += "\r\n";
                            count = 0;
                            if ((TouchReg[4] & 0x08) != 0) { RxStr += "D1 "; count++; }	//D1
                            if ((TouchReg[4] & 0x10) != 0) { RxStr += "D2 "; count++; }	//D2
                            if ((TouchReg[5] & 0x01) != 0) { RxStr += "D3 "; count++; }	//D3
                            if ((TouchReg[5] & 0x02) != 0) { RxStr += "D4 "; count++; }	//D4
                            if ((TouchReg[5] & 0x04) != 0) { RxStr += "D5 "; count++; }	//D5
                            if ((TouchReg[5] & 0x08) != 0) { RxStr += "D6 "; count++; }	//D6
                            if ((TouchReg[5] & 0x10) != 0) { RxStr += "D7 "; count++; }	//D7
                            if ((TouchReg[6] & 0x01) != 0) { RxStr += "D8 "; count++; }	//D8
                            if (count != 0) RxStr += "\r\n";
                            count = 0;
                            if ((TouchReg[6] & 0x02) != 0) { RxStr += "E1 "; count++; }	//E1
                            if ((TouchReg[6] & 0x04) != 0) { RxStr += "E2 "; count++; }	//E2
                            if ((TouchReg[6] & 0x08) != 0) { RxStr += "E3 "; count++; }	//E3
                            if ((TouchReg[6] & 0x10) != 0) { RxStr += "E4 "; count++; }	//E4
                            if ((TouchReg[7] & 0x01) != 0) { RxStr += "E5 "; count++; }	//E5
                            if ((TouchReg[7] & 0x02) != 0) { RxStr += "E6 "; count++; }	//E6
                            if ((TouchReg[7] & 0x04) != 0) { RxStr += "E7 "; count++; }	//E7
                            if ((TouchReg[7] & 0x08) != 0) { RxStr += "E8 "; count++; }	//E8	

                            Console.WriteLine(RxStr);
                            //Display_Image((UInt32)Index);
                            //label_TPValue.Text = RxStr;
                            SetTestValue(RxStr);
                            //---------------------------------------------------------------------------
                            NewFlag = false;
                            //---------------------------------------------------------------------------
                        }
                    }
                    //---------------------------------------------------------------------------------- Touch Test Mode End
                }
            }
            ///------------------------------------------------------------------------------------------
            catch (Exception ex)
            {
                Console.Write("Serial Port Thread Error:" + ex.Message);
            }
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== SetProcess====
        /// =============================================================================================
        delegate void SetTestValueCallback(string sText);
        private void SetTestValue(string sText)
        {
            // InvokeRequired required compares the thread ID of the 
            // calling thread to the thread ID of the creating thread. 
            // If these threads are different, it returns true. 
            if (this.btn_TouchTest.InvokeRequired)
            {
                SetTestValueCallback d = new SetTestValueCallback(SetTestValue);
                this.Invoke(d, new object[] { sText });
            }
            else
            {
                this.label_TPValue.Text = sText;
            }
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== Touch Screen   ====
        /// =============================================================================================
        private int TP_VAL_TAB_SIZE  =   34;
        //private int SensMin = 0;
        private int SensMax = 1000;
        private int ConfigIndex = 0;
        private int[] TP_SenBuffer = new int[34];
        private byte TP_Txd_CS = 0x00;
        private int ReadConfigTO = 0x00;
        private int WriteConfigTO = 0x00;

        readonly int[] TP_VAL_TAB = new int[]//34*4
        {
            //X Y	R	Index=A1  
            //X Y	R	Index=A1  
            360,	460,    30,	0,	 //0:D1
            250,	495,	60,	1,   //1:A8
            145,	545,	30,	2,   //2:D8
            100,	645,	60,	3,   //3:A7
             55,	760,	30,	4,   //4:D7
             90,	870,	60,	5,   //5:A6
            145,    975,	30,	6,   //6:D6
            250,   1030,	60,	7,   //7:A5
            360,   1060,	30,	8,   //8:D5
            470,   1030,	60,	9,   //9:A4
            575,	975,	30,	10,  //10:D4
            625,	870,	60,	11,  //11:A3
            665,	760,	30,	12,  //12:D3
            630,	645,	60,	13,  //13:A2
            575,	545,	30,	14,  //14:D2
            470,    495,    60, 15,  //15:A1
            360, 	550, 	30,	16,  //16:E1
            215, 	615, 	30,	17,  //17:E8
            155,	760 ,	30,	18,  //18:E7
            215,	900 ,	30,	19,  //19:E6
            360,	956,	30,	20,  //20:E5
            505,	900,	30,	21,  //21:E4
            570,	760,	30,	22,  //22:E3
            505 ,	615,	30,	23,  //23:E2
            300 ,	625,	30,	24,  //24:B8
            230 ,	700 ,	30,	25,  //25:B7
            230,	815 ,	30,	26,  //26:B6
            300,	900 ,	30,	27,  //27:B5
            415,	900,	30,	28,  //28:B4
            500,	815,	30,	29,  //29:B3
            500,	700,	30,	30,  //30:B2
            415,	625,	30,	31,  //31:B1
            330 ,	760,	30,	32,  //32:C2
            400 ,	760 ,	30,	33,  //33:C1
            /*
            300,	49,     30,	0,	 //0:D1
            207,	80,	    60,	1,   //1:A8
            120,	121,	30,	2,   //2:D8
            78,	    206,	60,	3,   //3:A7
            45,	    300,	30,	4,   //4:D7
            78 ,	389,	60,	5,   //5:A6
            120 ,	480,	30,	6,   //6:D6
            207,	520 ,	60,	7,   //7:A5
            300,	560 ,	30,	8,   //8:D5
            389,	520,	60,	9,   //9:A4
            480,	480,	30,	10,  //10:D4
            529,	389,	60,	11,  //11:A3
            560,	300,	30,	12,  //12:D3
            524  ,	206,	60,	13,  //13:A2
            480,	120,	30,	14,  //14:D2
            400,    80 ,    60, 15,  //15:A1
            300, 	130, 	30,	16,  //16:E1
            180, 	180, 	30,	17,  //17:E8
            130,	300 ,	30,	18,  //18:E7
            180,	420 ,	30,	19,  //19:E6
            300,	475,	30,	20,  //20:E5
            420,	420,	30,	21,  //21:E4
            475,	300,	30,	22,  //22:E3
            420 ,	180,	30,	23,  //23:E2
            255 ,	188,	30,	24,  //24:B8
            190 ,	250 ,	30,	25,  //25:B7
            190,	350 ,	30,	26,  //26:B6
            255,	415 ,	30,	27,  //27:B5
            350,	415,	30,	28,  //28:B4
            415,	350,	30,	29,  //29:B3
            415,	250,	30,	30,  //30:B2
            350 ,	188,	30,	31,  //31:B1
            270 ,	300,	30,	32,  //32:C2
            330 ,	300 ,	30,	33,  //33:C1
            */
        };

        //V1.0.0.26:20250412(1)ADX Seni Select 5Step//第一档：很低
        readonly int[] TP_SenDefault_1 = new int[]//34*4    
        {
          390, //0:D1
          705, //1:A8
          209, //2:D8
          705, //3:A7
          209, //4:D7
          705, //5:A6
          209, //6:D6
          705, //7:A5
          209, //8:D5
          705, //9:A4
          209, //10:D4
          705, //11:A3
          209, //12:D3
          705, //13:A2
          209, //14:D2
          705, //15:A1
          509, //16:E1
          509, //17:E8
          509, //18:E7
          509, //19:E6
          509, //20:E5
          509, //21:E4
          509, //22:E3
          509, //23:E2
          550, //24:B8
          550, //25:B7
          520, //26:B6
          520, //27:B5
          520, //28:B4
          520, //29:B3
          550, //30:B2
          550, //31:B1
          500, //32:C2
          500, //33:C1
     		};
        //V1.0.0.26:20250412(1)ADX Seni Select 5Step//第二档：较低
        readonly int[] TP_SenDefault_2 = new int[]//34*4
        {
          390, //0:D1
          655, //1:A8
          209, //2:D8
          655, //3:A7
          209, //4:D7
          655, //5:A6
          209, //6:D6
          655, //7:A5
          209, //8:D5
          655, //9:A4
          209, //10:D4
          655, //11:A3
          209, //12:D3
          655, //13:A2
          209, //14:D2
          655, //15:A1
          459, //16:E1
          459, //17:E8
          459, //18:E7
          459, //19:E6
          459, //20:E5
          459, //21:E4
          459, //22:E3
          459, //23:E2
          500, //24:B8
          500, //25:B7
          470, //26:B6
          470, //27:B5
          470, //28:B4
          470, //29:B3
          500, //30:B2
          500, //31:B1
          450, //32:C2
          450, //33:C1
     		};
        //V1.0.0.26:20250412(1)ADX Seni Select 5Step//第三档：中等
        readonly int[] TP_SenDefault_3 = new int[]//34*4
        {
          390, //0:D1
          605, //1:A8
          209, //2:D8
          605, //3:A7
          209, //4:D7
          605, //5:A6
          209, //6:D6
          605, //7:A5
          209, //8:D5
          605, //9:A4
          209, //10:D4
          605, //11:A3
          209, //12:D3
          605, //13:A2
          209, //14:D2
          605, //15:A1
          409, //16:E1
          409, //17:E8
          409, //18:E7
          409, //19:E6
          409, //20:E5
          409, //21:E4
          409, //22:E3
          409, //23:E2
          450, //24:B8
          450, //25:B7
          420, //26:B6
          420, //27:B5
          420, //28:B4
          420, //29:B3
          450, //30:B2
          450, //31:B1
          400, //32:C2
          400, //33:C1
     		};
        //V1.0.0.26:20250412(1)ADX Seni Select 5Step//第四档：较高
        readonly int[] TP_SenDefault_4 = new int[]//34*4
        {
          390, //0:D1
          505, //1:A8
          209, //2:D8
          505, //3:A7
          209, //4:D7
          505, //5:A6
          209, //6:D6
          505, //7:A5
          209, //8:D5
          505, //9:A4
          209, //10:D4
          505, //11:A3
          209, //12:D3
          505, //13:A2
          209, //14:D2
          505, //15:A1
          309, //16:E1
          309, //17:E8
          309, //18:E7
          309, //19:E6
          309, //20:E5
          309, //21:E4
          309, //22:E3
          309, //23:E2
          350, //24:B8
          350, //25:B7
          320, //26:B6
          320, //27:B5
          320, //28:B4
          320, //29:B3
          350, //30:B2
          350, //31:B1
          300, //32:C2
          300, //33:C1
     		};
        //V1.0.0.26:20250412(1)ADX Seni Select 5Step//第五档：很高
        readonly int[] TP_SenDefault_5 = new int[]//34*4
        {
          390, //0:D1
          418, //1:A8
          209, //2:D8
          418, //3:A7
          209, //4:D7
          418, //5:A6
          209, //6:D6
          418, //7:A5
          209, //8:D5
          418, //9:A4
          209, //10:D4
          418, //11:A3
          209, //12:D3
          418, //13:A2
          209, //14:D2
          418, //15:A1
          187, //16:E1
          187, //17:E8
          187, //18:E7
          187, //19:E6
          187, //20:E5
          187, //21:E4
          187, //22:E3
          187, //23:E2
          209, //24:B8
          209, //25:B7
          209, //26:B6
          209, //27:B5
          209, //28:B4
          209, //29:B3
          209, //30:B2
          209, //31:B1
          203, //32:C2
          203, //33:C1
     		};
     		

        readonly int[] TP_SenDefault = new int[]//34*4
        {
          //灵敏度设置: 0~1000, 数值越小越灵敏
          ///------------------------------------------------------------------------------------------ Level1
          390, //0:D1
          418, //1:A8
          209, //2:D8
          418, //3:A7
          209, //4:D7
          418, //5:A6
          209, //6:D6
          418, //7:A5
          209, //8:D5
          418, //9:A4
          209, //10:D4
          418, //11:A3
          209, //12:D3
          418, //13:A2
          209, //14:D2
          418, //15:A1
          187, //16:E1
          187, //17:E8
          187, //18:E7
          187, //19:E6
          187, //20:E5
          187, //21:E4
          187, //22:E3
          187, //23:E2
          209, //24:B8
          209, //25:B7
          209, //26:B6
          209, //27:B5
          209, //28:B4
          209, //29:B3
          209, //30:B2
          209, //31:B1
          203, //32:C2
          203, //33:C1

//          100, //0:D1
//          101, //1:A8
//          102, //2:D8
//          103, //3:A7
//          104, //4:D7
//          105, //5:A6
//          106, //6:D6
//          107, //7:A5
//          108, //8:D5
//          109, //9:A4
//          110, //10:D4
//          111, //11:A3
//          112, //12:D3
//          113, //13:A2
//          114, //14:D2
//          115, //15:A1
//          116, //16:E1
//          117, //17:E8
//          118, //18:E7
//          119, //19:E6
//          120, //20:E5
//          121, //21:E4
//          122, //22:E3
//          123, //23:E2
//          124, //24:B8
//          125, //25:B7
//          126, //26:B6
//          127, //27:B5
//          128, //28:B4
//          129, //29:B3
//          130, //30:B2
//          131, //31:B1
//          132, //32:C2
//          133, //33:C1
          
        };
        //V1.0.0.18:20241012(1)Change ADX Save Issue
        readonly int[] TP_SenDefault_N2 = new int[]//34*4
        {
          //灵敏度设置: 0~1000, 数值越小越灵敏
          390-100, //0:D1
          418-100, //1:A8
          209-100, //2:D8
          418-100, //3:A7
          209-100, //4:D7
          418-100, //5:A6
          209-100, //6:D6
          418-100, //7:A5
          209-100, //8:D5
          418-100, //9:A4
          209-100, //10:D4
          418-100, //11:A3
          209-100, //12:D3
          418-100, //13:A2
          209-100, //14:D2
          418-100, //15:A1
          187-100, //16:E1
          187-100, //17:E8
          187-100, //18:E7
          187-100, //19:E6
          187-100, //20:E5
          187-100, //21:E4
          187-100, //22:E3
          187-100, //23:E2
          209-100, //24:B8
          209-100, //25:B7
          209-100, //26:B6
          209-100, //27:B5
          209-100, //28:B4
          209-100, //29:B3
          209-100, //30:B2
          209-100, //31:B1
          203-100, //32:C2
          203-100, //33:C1 
        };
        //V1.0.0.18:20241012(1)Change ADX Save Issue
        readonly int[] TP_SenDefault_N1 = new int[]//34*4
        {
          //灵敏度设置: 0~1000, 数值越小越灵敏
          390-60, //0:D1
          418-60, //1:A8
          209-60, //2:D8
          418-60, //3:A7
          209-60, //4:D7
          418-60, //5:A6
          209-60, //6:D6
          418-60, //7:A5
          209-60, //8:D5
          418-60, //9:A4
          209-60, //10:D4
          418-60, //11:A3
          209-60, //12:D3
          418-60, //13:A2
          209-60, //14:D2
          418-60, //15:A1
          187-60, //16:E1
          187-60, //17:E8
          187-60, //18:E7
          187-60, //19:E6
          187-60, //20:E5
          187-60, //21:E4
          187-60, //22:E3
          187-60, //23:E2
          209-60, //24:B8
          209-60, //25:B7
          209-60, //26:B6
          209-60, //27:B5
          209-60, //28:B4
          209-60, //29:B3
          209-60, //30:B2
          209-60, //31:B1
          203-60, //32:C2
          203-60, //33:C1 
        };
        //V1.0.0.18:20241012(1)Change ADX Save Issue
        readonly int[] TP_SenDefault_P1 = new int[]//34*4
        {
          //灵敏度设置: 0~1000, 数值越小越灵敏
          390+60, //0:D1
          418+60, //1:A8
          209+60, //2:D8
          418+60, //3:A7
          209+60, //4:D7
          418+60, //5:A6
          209+60, //6:D6
          418+60, //7:A5
          209+60, //8:D5
          418+60, //9:A4
          209+60, //10:D4
          418+60, //11:A3
          209+60, //12:D3
          418+60, //13:A2
          209+60, //14:D2
          418+60, //15:A1
          187+60, //16:E1
          187+60, //17:E8
          187+60, //18:E7
          187+60, //19:E6
          187+60, //20:E5
          187+60, //21:E4
          187+60, //22:E3
          187+60, //23:E2
          209+60, //24:B8
          209+60, //25:B7
          209+60, //26:B6
          209+60, //27:B5
          209+60, //28:B4
          209+60, //29:B3
          209+60, //30:B2
          209+60, //31:B1
          203+60, //32:C2
          203+60, //33:C1 
        };
        //V1.0.0.18:20241012(1)Change ADX Save Issue
        readonly int[] TP_SenDefault_P2 = new int[]//34*4
        {
          //灵敏度设置: 0~1000, 数值越小越灵敏
					390+100, //0:D1
          418+100, //1:A8
          209+100, //2:D8
          418+100, //3:A7
          209+100, //4:D7
          418+100, //5:A6
          209+100, //6:D6
          418+100, //7:A5
          209+100, //8:D5
          418+100, //9:A4
          209+100, //10:D4
          418+100, //11:A3
          209+100, //12:D3
          418+100, //13:A2
          209+100, //14:D2
          418+100, //15:A1
          187+100, //16:E1
          187+100, //17:E8
          187+100, //18:E7
          187+100, //19:E6
          187+100, //20:E5
          187+100, //21:E4
          187+100, //22:E3
          187+100, //23:E2
          209+100, //24:B8
          209+100, //25:B7
          209+100, //26:B6
          209+100, //27:B5
          209+100, //28:B4
          209+100, //29:B3
          209+100, //30:B2
          209+100, //31:B1
          203+100, //32:C2
          203+100, //33:C1 
        };
        
                
        /// =============================================================================================
        /// ==== Update CRC   ====
        /// =============================================================================================
        private byte UPDATE_CRC(byte[] Buffer, byte Len)
        {
            ///------------------------------------------------------------------------------------------
            byte i = 0, j = 0, crc = 0;
            ///------------------------------------------------------------------------------------------	
            for (j = 0; j < Len; j++)
            {
                crc ^= Buffer[j];
                for (i = 0; i < 8; i++)
                {
                    if ((crc & 0x01) != 0)
                    {
                        crc = (byte)((crc >> 1) ^ 0x8C);
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            ///------------------------------------------------------------------------------------------
            return crc;
            ///------------------------------------------------------------------------------------------	
        }
        /// =============================================================================================
        /// ==== Get Index Name ====
        /// =============================================================================================
        private string GetIndexName(UInt32 Index)
        {
            ///------------------------------------------------------------------------------------------
            string Name = "";
            ///------------------------------------------------------------------------------------------
            if (Index == 0) Name = "D1";
            else if (Index == 1) Name = "A8";
            else if (Index == 2) Name = "D8";
            else if (Index == 3) Name = "A7";
            else if (Index == 4) Name = "D7";
            else if (Index == 5) Name = "A6";
            else if (Index == 6) Name = "D6";
            else if (Index == 7) Name = "A5";
            else if (Index == 8) Name = "D5";
            else if (Index == 9) Name = "A4";
            else if (Index == 10) Name = "D4";
            else if (Index == 11) Name = "A3";
            else if (Index == 12) Name = "D3";
            else if (Index == 13) Name = "A2";
            else if (Index == 14) Name = "D2";
            else if (Index == 15) Name = "A1";
            else if (Index == 16) Name = "E1";
            else if (Index == 17) Name = "E8";
            else if (Index == 18) Name = "E7";
            else if (Index == 19) Name = "E6";
            else if (Index == 20) Name = "E5";
            else if (Index == 21) Name = "E4";
            else if (Index == 22) Name = "E3";
            else if (Index == 23) Name = "E2";
            else if (Index == 24) Name = "B8";
            else if (Index == 25) Name = "B7";
            else if (Index == 26) Name = "B6";
            else if (Index == 27) Name = "B5";
            else if (Index == 28) Name = "B4";
            else if (Index == 29) Name = "B3";
            else if (Index == 30) Name = "B2";
            else if (Index == 31) Name = "B1";
            else if (Index == 32) Name = "C2";
            else if (Index == 33) Name = "C1";
            ///------------------------------------------------------------------------------------------
            return Name;
            ///------------------------------------------------------------------------------------------
        }


        /// =============================================================================================
        /// ==== pictureBox1 Display ====
        /// =============================================================================================
        private void Display_Image(UInt32 ImageIndex)
        {
            ///------------------------------------------------------------------------------------------
            //PictureBox myPictureBox = pictureBox;
            //ToyName = ToyListTable[ImageIndex].ToyNa + "\r\n" + "ToyID = " + ToyListTable[ImageIndex].ToyID + "\r\n" + "SubID = " + ToyListTable[ImageIndex].SubID;
            //string ToyIDFileName1 = ToyListTable[ImageIndex].ToyID.ToString() + "_" + ToyListTable[ImageIndex].FraID.ToString() + "_" + ToyListTable[ImageIndex].SubID.ToString() + ".JPG";
            //string ToyIDFileName2 = ToyListTable[ImageIndex].ToyID.ToString() + "_" + ToyListTable[ImageIndex].FraID.ToString() + "_" + ToyListTable[ImageIndex].SubID.ToString() + ".jpg";
            ///------------------------------------------------------------------------------------------
            //string ImageFileName = "BG";
            //if (ImageIndex >= 0 && ImageIndex <= 7) ImageFileName = "A" + (ImageIndex + 1).ToString();
            //else if (ImageIndex >= 8 && ImageIndex <= 15) ImageFileName = "B" + ((ImageIndex - 8) + 1).ToString();
            //else if (ImageIndex == 16) ImageFileName = "C1";
            //else if (ImageIndex == 17) ImageFileName = "C2";
            //else if (ImageIndex >= 18 && ImageIndex <= 25) ImageFileName = "D" + ((ImageIndex - 18) + 1).ToString();
            //else if (ImageIndex >= 26 && ImageIndex <= 33) ImageFileName = "E" + ((ImageIndex - 26) + 1).ToString();
            ///------------------------------------------------------------------------------------------
            string ImageFileName = "BG"; 
            ImageFileName = GetIndexName(ImageIndex);
            if (ImageFileName == "") ImageFileName = "BG";
            ///------------------------------------------------------------------------------------------
            string FileName1 = ImageFileName+".PNG";
            string FileName2 = ImageFileName + ".png";
            ///------------------------------------------------------------------------------------------
            //myPictureBox.Image = global::ConfigAppNS.Properties.Resources.A1;   //global::Config.Properties.Resources.NoName;
            if (files != null)
            {
                foreach (System.IO.FileInfo file in files)
                {
                    if ((file.Name == FileName1) || (file.Name == FileName2))
                    {
                        pictureBox.BackgroundImage = System.Drawing.Image.FromFile(file.FullName);
                        //pictureBox.BackgroundImageLayout = ImageLayout.Zoom;
                        break;
                    }
                }
            }
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Touch Aera check ====
        /// =============================================================================================
        private int App_Touch_Point_Check(int TPX,int TPY,int X0,int Y0,int R0)
        {
            ///------------------------------------------------------------------------------------------
	        int XX,YY;
            ///------------------------------------------------------------------------------------------
            XX = ((TPX - X0) * (TPX - X0)) + ((TPY - Y0) * (TPY - Y0));
            YY = R0*R0;
            ///------------------------------------------------------------------------------------------
            if(XX<=YY)return 1;
            ///------------------------------------------------------------------------------------------
            return 0;
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Get Touch Index ====
        /// =============================================================================================
        private int App_Touch_Get_IndexVal(int X, int Y)		
        {
            ///------------------------------------------------------------------------------------------	
            int i, Index = 0xff;
            ///------------------------------------------------------------------------------------------
	        for(i=0;i<TP_VAL_TAB_SIZE;i++)
            {
                if(App_Touch_Point_Check(X,Y,TP_VAL_TAB[i*4+0],TP_VAL_TAB[i*4+1],TP_VAL_TAB[i*4+2])!=0)
                {
                    Index = TP_VAL_TAB[i*4+3];
                    ConfigIndex = Index;
                    string Name = GetIndexName((UInt32)ConfigIndex);
                    //groupBox_Index.Text = Name+"("+Index.ToString() +"),Loc="+ string.Format("X:{0}  Y:{1}", X, Y);
                    Display_Image((UInt32)Index);
                    Display_Current_Sens((UInt32)Index);
                    groupBox_Edit.Visible = true;
                    trackBar_Val.Enabled = true;
                }
            }
            ///------------------------------------------------------------------------------------------
            return Index;
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== PictureBox Click ====
        /// =============================================================================================
        private void pictureBox_Click(object sender, EventArgs e)
        {
            if (bTouchTestMode == false)
            {
                Point mousePosition = System.Windows.Forms.Cursor.Position;
                //string str = "pictureBox:" + pictureBox.Location.X.ToString() + "," + pictureBox.Location.Y.ToString() + "," + pictureBox.Size.Width.ToString() + "," + pictureBox.Size.Height.ToString();
                //Console.WriteLine(str); 
                int LabHeight = (this.Height - this.ClientRectangle.Height) + groupBox_Edit.Height;
                int Y = (mousePosition.Y - LabHeight);
                if (Y >= 0)
                {
                    //Logtxtbx.Text = string.Format("X:{0}  Y:{1}", mousePosition.X, mousePosition.Y);
                    //label_Pkt_Count.Text = "Loc=" + string.Format("X:{0}  Y:{1}", mousePosition.X, Y);
                    //label_L_FW.Text = string.Format("X:{0}  Y:{1}", mousePosition.X, mousePosition.Y);
                    App_Touch_Get_IndexVal(mousePosition.X, Y);
                }
            }
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== Current Value Display ====
        /// =============================================================================================
        private void Display_Current_Sens(UInt32 Index)
        {
            int SensVal = 0;
            double Percent = 0.0;
            string Name = GetIndexName((UInt32)Index);
            SensVal = TP_SenBuffer[Index];
            Percent = (double)(SensVal);
            Percent *= (double)(100.0);
            Percent /= (double)(SensMax);
            trackBar_Val.Value = (int)SensVal;

            //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
            bool bGroupName = false;
            string GroupName = "";
            if (checkBox_A.Checked == true)
            {
                bGroupName = true;
                GroupName += "A ";
            }
            if (checkBox_B.Checked == true)
            {
                bGroupName = true;
                GroupName += "B ";
            }
            if (checkBox_C.Checked == true)
            {
                bGroupName = true;
                GroupName += "C ";
            }
            if (checkBox_D.Checked == true)
            {
                bGroupName = true;
                GroupName += "D ";
            }
            if (checkBox_E.Checked == true)
            {
                bGroupName = true;
                GroupName += "E ";
            }

            if (bGroupName == true)
            {
                label_Val.Text = GroupName + " ( All ) ( "+Name + " )=" + Percent.ToString("F1") + "% (" + SensVal + ")";
            }
            else
            {
                label_Val.Text = Name + "=" + Percent.ToString("F1") + "% (" + SensVal + ")";
            }
        }

        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        private void Load_Default_Sens()
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault[i];
            }	
        }

        /// =============================================================================================
        /// ==== Sens Value changed  ====
        /// =============================================================================================
        private void trackBar_Val_Scroll(object sender, EventArgs e)
        {
            int SensVal = trackBar_Val.Value;
            double Percent=0.0;
            string Name = GetIndexName((UInt32)ConfigIndex);
            TP_SenBuffer[ConfigIndex] = SensVal;
            Percent  = (double)(SensVal);
            Percent *= 100.0;
            Percent /= (double)SensMax;
            label_Val.Text = Name + "=" + Percent.ToString("F1") + "% (" + SensVal + ")";

            //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
            //V1.0.0.23:20250322:(1)ADX Sens ADJ in Group All/A/B/C/D/E
            if (checkBox_All.Checked == true)
            {
                TP_SenBuffer[0] = SensVal;//390, //0:D1
                TP_SenBuffer[1] = SensVal;//418, //1:A8
                TP_SenBuffer[2] = SensVal;//209, //2:D8
                TP_SenBuffer[3] = SensVal;//418, //3:A7
                TP_SenBuffer[4] = SensVal;//209, //4:D7
                TP_SenBuffer[5] = SensVal;//418, //5:A6
                TP_SenBuffer[6] = SensVal;//209, //6:D6
                TP_SenBuffer[7] = SensVal;//418, //7:A5
                TP_SenBuffer[8] = SensVal;//209, //8:D5
                TP_SenBuffer[9] = SensVal;//418, //9:A4
                TP_SenBuffer[10] = SensVal;//209, //10:D4
                TP_SenBuffer[11] = SensVal;//418, //11:A3
                TP_SenBuffer[12] = SensVal;//209, //12:D3
                TP_SenBuffer[13] = SensVal;//418, //13:A2
                TP_SenBuffer[14] = SensVal;//209, //14:D2
                TP_SenBuffer[15] = SensVal;//418, //15:A1
                TP_SenBuffer[16] = SensVal;//187, //16:E1
                TP_SenBuffer[17] = SensVal;//187, //17:E8
                TP_SenBuffer[18] = SensVal;//187, //18:E7
                TP_SenBuffer[19] = SensVal;//187, //19:E6
                TP_SenBuffer[20] = SensVal;//187, //20:E5
                TP_SenBuffer[21] = SensVal;//187, //21:E4
                TP_SenBuffer[22] = SensVal;//187, //22:E3
                TP_SenBuffer[23] = SensVal;//187, //23:E2
                TP_SenBuffer[24] = SensVal;//209, //24:B8
                TP_SenBuffer[25] = SensVal;//209, //25:B7
                TP_SenBuffer[26] = SensVal;//209, //26:B6
                TP_SenBuffer[27] = SensVal;//209, //27:B5
                TP_SenBuffer[28] = SensVal;//209, //28:B4
                TP_SenBuffer[29] = SensVal;//209, //29:B3
                TP_SenBuffer[30] = SensVal;//209, //30:B2
                TP_SenBuffer[31] = SensVal;//209, //31:B1
                TP_SenBuffer[32] = SensVal;//203, //32:C2
                TP_SenBuffer[33] = SensVal;//203, //33:C1
            }
            else
            {
                if (checkBox_A.Checked == true)
                {
                    TP_SenBuffer[1] = SensVal;//418, //1:A8
                    TP_SenBuffer[3] = SensVal;//418, //3:A7
                    TP_SenBuffer[5] = SensVal;//418, //5:A6
                    TP_SenBuffer[7] = SensVal;//418, //7:A5
                    TP_SenBuffer[9] = SensVal;//418, //9:A4
                    TP_SenBuffer[11] = SensVal;//418, //11:A3
                    TP_SenBuffer[13] = SensVal;//418, //13:A2
                    TP_SenBuffer[15] = SensVal;//418, //15:A1
                }
                if (checkBox_B.Checked == true)
                {
                    TP_SenBuffer[24] = SensVal;//209, //24:B8
                    TP_SenBuffer[25] = SensVal;//209, //25:B7
                    TP_SenBuffer[26] = SensVal;//209, //26:B6
                    TP_SenBuffer[27] = SensVal;//209, //27:B5
                    TP_SenBuffer[28] = SensVal;//209, //28:B4
                    TP_SenBuffer[29] = SensVal;//209, //29:B3
                    TP_SenBuffer[30] = SensVal;//209, //30:B2
                    TP_SenBuffer[31] = SensVal;//209, //31:B1
                }
                if (checkBox_C.Checked == true)
                {
                    TP_SenBuffer[32] = SensVal;//203, //32:C2
                    TP_SenBuffer[33] = SensVal;//203, //33:C1
                }
                if (checkBox_D.Checked == true)
                {
                    TP_SenBuffer[0] = SensVal;//390, //0:D1
                    TP_SenBuffer[2] = SensVal;//209, //2:D8
                    TP_SenBuffer[4] = SensVal;//209, //4:D7
                    TP_SenBuffer[6] = SensVal;//209, //6:D6
                    TP_SenBuffer[8] = SensVal;//209, //8:D5
                    TP_SenBuffer[10] = SensVal;//209, //10:D4
                    TP_SenBuffer[12] = SensVal;//209, //12:D3
                    TP_SenBuffer[14] = SensVal;//209, //14:D2
                }
                if (checkBox_E.Checked == true)
                {
                    TP_SenBuffer[16] = SensVal;//187, //16:E1
                    TP_SenBuffer[17] = SensVal;//187, //17:E8
                    TP_SenBuffer[18] = SensVal;//187, //18:E7
                    TP_SenBuffer[19] = SensVal;//187, //19:E6
                    TP_SenBuffer[20] = SensVal;//187, //20:E5
                    TP_SenBuffer[21] = SensVal;//187, //21:E4
                    TP_SenBuffer[22] = SensVal;//187, //22:E3
                    TP_SenBuffer[23] = SensVal;//187, //23:E2
                }
            }
        }

        /// =============================================================================================
        /// ==== Btn Click: Default ====
        /// =============================================================================================
        private void btn_default_Click(object sender, EventArgs e)
        {
            //V1.0.0.26:20250412(1)ADX Seni Select
            /*
            Load_Default_Sens();
            //Display_Image(0xFFFF);
            //trackBar_Val.Value = 0;
            //trackBar_Val.Enabled = false;
            //label_Val.Text = "";
            Display_Current_Sens((UInt32)ConfigIndex);     
            //MessageBox.Show("Default Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            */
            //V1.0.0.26:20250412(1)ADX Seni Display
            if (bLanguageEN == true)
            {
                groupBox_Sensi.Text = "Sensitivity preset";
            }
            else
            {
                groupBox_Sensi.Text = "灵敏度预设";
            }

            if (groupBox_Sensi.Visible == false)
            {
                groupBox_Sensi.Visible = true;
                groupBox_Mode.Visible = false;
            }
            else
            {
                groupBox_Sensi.Visible = false;
                groupBox_Mode.Visible = true;
            }
        }
        //V1.0.0.26:20250412(1)ADX Seni Select
        private void BtnSensi1_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_1[i];
            }	
            Display_Current_Sens((UInt32)ConfigIndex);   
        }
        //V1.0.0.26:20250412(1)ADX Seni Select
        private void BtnSensi2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_2[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex);   
        }
        //V1.0.0.26:20250412(1)ADX Seni Select
        private void BtnSensi3_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_3[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex);   
        }
        //V1.0.0.26:20250412(1)ADX Seni Select
        private void BtnSensi4_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_4[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex);   
        }
        //V1.0.0.26:20250412(1)ADX Seni Select
        private void BtnSensi5_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_5[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex);   
        }

        private void Btn_Default_S1_Click(object sender, EventArgs e)	//All Sensitivity -2
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_N2[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex);   
        }

        private void Btn_Default_S2_Click(object sender, EventArgs e)	//All Sensitivity -1
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_N1[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex); 
        }

        private void Btn_Default_S3_Click(object sender, EventArgs e)	//All Sensitivity +1
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_P1[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex); 
        }

        private void Btn_Default_S4_Click(object sender, EventArgs e)	//All Sensitivity +2
        {
            for (int i = 0; i < TP_VAL_TAB_SIZE; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault_P2[i];
            }
            Display_Current_Sens((UInt32)ConfigIndex); 
        }
        
        /// =============================================================================================
        /// ==== Read Value Btn Click  ====
        ///CMD : 52 45 41 44 00
        ///RSP : 52 52 53 50 00
        ///Data: 01 86 01 E0 01 2C 01E0 012C 01E0 012C 01E0 012C 01E0 012C 01E0 012C 01E0 012C 01E0 015E 
        ///CS  : 66	
        ///CMD : 52 45 41 44 01
        ///RSP : 52 52 53 50 01 
        ///Data: 01 5E 01 5E 01 5E 015E 015E 015E 015E 0186 0186 0172 0172 0172 0172 0186 0186 015E 015E 
        ///CS  : 5C
        /// =============================================================================================
        private void btn_Read_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
            /*
            btn_TouchTest.Enabled = true;
            btn_ButtonTest.Enabled = true;
            btn_default.Enabled = true;
            btn_Read.Enabled = true;
            btn_Save.Enabled = true;
            Btn_Delay_Config.Enabled = true;

            Btn_ReadMode.Enabled = true;
            Btn_WriteMode.Enabled = true;

            Btn_Sens_Open.Enabled = true;
            Btn_Sens_Save.Enabled = true;

            checkBox_A.Enabled = true;
            checkBox_B.Enabled = true;
            checkBox_C.Enabled = true;
            checkBox_D.Enabled = true;
            checkBox_E.Enabled = true;

            Btn_RGB.Enabled = true; /// V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
            */
            //-------------------------------------------------------------------------------------------
            //Open_Serial_Port();
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;  
            byte index = 0;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            byte PktNum = 12;	//V1.0.0.18:20241012(1)Change ADX Save Issue
            //byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_SENS);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            SendByteBuf[index++] = (byte)(0x00);
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[1] == 0x52 &&
                InputReportBuf[2] == 0x53 &&
                InputReportBuf[3] == 0x50 &&
                InputReportBuf[4] == 0x00)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
                byte SyncLen = 5;
                byte DataLen = (byte)(PktNum * 2);						//V1.0.0.18:20241012(1)Change ADX Save Issue
                byte CSlen   = 1;
                byte PktLen  = (byte)(SyncLen + DataLen + CSlen);
                byte CS = UPDATE_CRC(InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[PktLen - 1])
                {
                    for (int i = 0; i < PktNum; i++)	//V1.0.0.18:20241012(1)Change ADX Save Issue
                    {
                        TP_SenBuffer[i] = ((InputReportBuf[SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[SyncLen + (i * 2 + 1)]));
                    }
                    //MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_SENS);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            SendByteBuf[index++] = (byte)(0x01);
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[1] == 0x52 &&
                InputReportBuf[2] == 0x53 &&
                InputReportBuf[3] == 0x50 &&
                InputReportBuf[4] == 0x01)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
                byte SyncLen = 5;
                byte DataLen = (byte)(PktNum * 2);							//V1.0.0.18:20241012(1)Change ADX Save Issue
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = UPDATE_CRC(InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[PktLen - 1])
                {
                    for (int i = 0; i < PktNum; i++)		//V1.0.0.18:20241012(1)Change ADX Save Issue
                    {
                        TP_SenBuffer[PktNum+i] = ((InputReportBuf[SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[SyncLen + (i * 2 + 1)]));
                    }
                    //MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //------------------------------------------------------------------------------------------- //V1.0.0.18:20241012(1)Change ADX Save Issue
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_SENS);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            SendByteBuf[index++] = (byte)(0x02);
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[1] == 0x52 &&
                InputReportBuf[2] == 0x53 &&
                InputReportBuf[3] == 0x50 &&
                InputReportBuf[4] == 0x02)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
                byte SyncLen = 5;
                byte DataLen = (byte)((PktNum-2) * 2);							//V1.0.0.18:20241012(1)Change ADX Save Issue
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = UPDATE_CRC(InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[PktLen - 1])
                {
                    for (int i = 0; i < (PktNum-2); i++)		//V1.0.0.18:20241012(1)Change ADX Save Issue
                    {
                        TP_SenBuffer[PktNum*2+i] = ((InputReportBuf[SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[SyncLen + (i * 2 + 1)]));
                    }
                    //MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Read Config Error !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            Display_Image(0xFFFF);
            trackBar_Val.Value = 0;
            trackBar_Val.Enabled = false;
            label_Val.Text = "";
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
            btn_TouchTest.Enabled = true;
            btn_ButtonTest.Enabled = true;
            btn_default.Enabled = true;
            btn_Read.Enabled = true;
            btn_Save.Enabled = true;
            Btn_Delay_Config.Enabled = true;

            Btn_ReadMode.Enabled = true;
            Btn_WriteMode.Enabled = true;

            Btn_Sens_Open.Enabled = true;
            Btn_Sens_Save.Enabled = true;

            checkBox_A.Enabled = true;
            checkBox_B.Enabled = true;
            checkBox_C.Enabled = true;
            checkBox_D.Enabled = true;
            checkBox_E.Enabled = true;

            Btn_RGB.Enabled = true; /// V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Save Value Btn Click  ====
        ///CMD : 57 52 49 54 00
        ///Data: 01 3F 01 A2 00 D1 01 A2 00 D1 01 A2 00 D1 01 A2 00 D1 01 A2 00 D1 01 A2 00 D1 01 A2 00 D1 01 A2 00 BB 
        ///CS  : FA
        ///RSP : 57 4F 4B FA 00 
        ///CMD : 57 52 49 54 01
        ///Data: 00 BB 00 BB 00 BB 00 BB 00 BB 00 BB 00 BB 00 D1 00 D1 00 D1 00 D1 00 D1 00 D1 00 D1 00 D1 00 CB 00 CB 
        ///CS  : 77			
        ///RSP : 57 4F 4B 77 01 
        /// =============================================================================================
        private void btn_Save_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            //Open_Serial_Port();
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            byte PktNum = 12;	//V1.0.0.18:20241012(1)Change ADX Save Issue
            //byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_SENS);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            SendByteBuf[index++] = (byte)(0x00);    //Index
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < PktNum; i++)	//V1.0.0.18:20241012(1)Change ADX Save Issue
            {
                SendByteBuf[index++] = (byte)((TP_SenBuffer[i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((TP_SenBuffer[i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            TP_Txd_CS = UPDATE_CRC(SendByteBuf, index);
            SendByteBuf[index++] = TP_Txd_CS;
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[1] == 0x4F &&
                InputReportBuf[2] == 0x4B &&
                InputReportBuf[4] == 0x00)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[3].ToString()+")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_SENS);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            SendByteBuf[index++] = (byte)(0x01);    //Index
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < PktNum; i++)		//V1.0.0.18:20241012(1)Change ADX Save Issue
            {
                SendByteBuf[index++] = (byte)((TP_SenBuffer[PktNum + i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((TP_SenBuffer[PktNum + i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            TP_Txd_CS = UPDATE_CRC(SendByteBuf, index);
            SendByteBuf[index++] = TP_Txd_CS;
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[1] == 0x4F &&
                InputReportBuf[2] == 0x4B &&
                InputReportBuf[4] == 0x01)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[3].ToString() + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------//V1.0.0.18:20241012(1)Change ADX Save Issue
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_SENS);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            SendByteBuf[index++] = (byte)(0x02);    //Index
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < PktNum-2; i++)		//V1.0.0.18:20241012(1)Change ADX Save Issue
            {
                SendByteBuf[index++] = (byte)((TP_SenBuffer[PktNum*2 + i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((TP_SenBuffer[PktNum*2 + i] >> 0) & 0xff);
            }
            SendByteBuf[index++] = 0;			//Dumy Data
            SendByteBuf[index++] = 0;			//
            SendByteBuf[index++] = 0;			//Dumy Data
            SendByteBuf[index++] = 0;			//
            //-------------------------------------------------------------------------------------------
            TP_Txd_CS = UPDATE_CRC(SendByteBuf, index);
            SendByteBuf[index++] = TP_Txd_CS;
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[1] == 0x4F &&
                InputReportBuf[2] == 0x4B &&
                InputReportBuf[4] == 0x02)
            {
                //---------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < 64; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //---------------------------------------------------------------------------------------
            }
            else
            {
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[3].ToString() + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            Display_Image(0xFFFF);
            trackBar_Val.Value = 0;
            trackBar_Val.Enabled = false;
            label_Val.Text = "";
            MessageBox.Show("Write Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ====TouchScreen Test Btn Click====
        /// =============================================================================================
        private void btn_StartTest_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            //Open_Serial_Port();   //V1.0.0.30:20250510(1)ADX COM Port Release
            //-------------------------------------------------------------------------------------------
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            if (bTouchTestMode == false)
            {
                Open_Serial_Port();    //V1.0.0.30:20250510(1)ADX COM Port Release

                bTouchTestMode = true;
                SendByteBuf[index++] = (byte)(0x7B);    //SYNC H   7B 52 53 41 54 7D 
                SendByteBuf[index++] = (byte)(0x52);    //SYNC L
                SendByteBuf[index++] = (byte)(0x53);
                SendByteBuf[index++] = (byte)(0x41);
                SendByteBuf[index++] = (byte)(0x54);
                SendByteBuf[index++] = (byte)(0x7D);

                label_TPValue.Text = "";
                groupBox_Mode.Visible = false;
                label_Pkt_Count.Visible = false;
                //groupBox_Index.Visible = false;
                pictureBox.Visible = false;
                panel1.Visible = true;
                panel2.Visible = false;

                btn_ButtonTest.Enabled = false;
                if (bLanguageEN == true)
                {
                    btn_TouchTest.Text = "StopTest";
                }
                else
                {
                    btn_TouchTest.Text = "停止测试";
                }
            }
            else
            {
                if (MyCom.SerialStatus == true)  //V1.0.0.30:20250510(1)ADX COM Port Release
                {
                    Close_Serial_Port();   
                }

                bTouchTestMode = false;
                SendByteBuf[index++] = (byte)(0x7B);    //SYNC H   7B 48 41 4C 54 7D
                SendByteBuf[index++] = (byte)(0x48);    //SYNC L
                SendByteBuf[index++] = (byte)(0x41);
                SendByteBuf[index++] = (byte)(0x4C);
                SendByteBuf[index++] = (byte)(0x54);
                SendByteBuf[index++] = (byte)(0x7D);

                label_TPValue.Text = "";
                groupBox_Mode.Visible = true;
                label_Pkt_Count.Visible = true;
                //groupBox_Index.Visible = true;
                pictureBox.Visible = true;
                panel1.Visible = false;
                panel2.Visible = false;

                btn_ButtonTest.Enabled = true;
                if (bLanguageEN == true)
                {
                    btn_TouchTest.Text = "TouchTest";
                }
                else
                {
                    btn_TouchTest.Text = "触摸屏测试";
                }
            }
            //-------------------------------------------------------------------------------------------
            Display_Image(0xFFFF);
            trackBar_Val.Value = 0;
            trackBar_Val.Enabled = false;
            label_Val.Text = "";
            //-------------------------------------------------------------------------------------------
            if (MyCom.SerialStatus == true)
            {
                MyCom.WriteSerialPortHex(SendByteBuf, 0, index);
                PictureBox_Disable_all();
            }
            else
            {
                Console.WriteLine("Invalid Com Port"); //MessageBox.Show("Invalid Com Port \r\n", "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            //-------------------------------------------------------------------------------------------
        }


        private void btn_ButtonTest_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            if (bButtonTestMode == false)
            {
                bButtonTestMode = true;
                label_TPValue.Text = "";
                groupBox_Mode.Visible = false;
                label_Pkt_Count.Visible = false;
                pictureBox.Visible = false;
                panel1.Visible = false;
                panel2.Visible = true;

                btn_TouchTest.Enabled = false;
                if (bLanguageEN == true)
                {
                    btn_ButtonTest.Text = "StopTest";
                }
                else
                {
                    btn_ButtonTest.Text = "停止测试";
                }
            }
            else
            {
                bButtonTestMode = false;
                label_TPValue.Text = "";
                groupBox_Mode.Visible = true;
                label_Pkt_Count.Visible = true;
                pictureBox.Visible = true;
                panel1.Visible = false;
                panel2.Visible = false;

                btn_TouchTest.Enabled = true;
                if (bLanguageEN == true)
                {
                    btn_ButtonTest.Text = "ButtonTest";
                }
                else
                {
                    btn_ButtonTest.Text = "按键测试";
                }
            }
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        
        /// =============================================================================================
        /// ==== radioBtn1P Click  ====
        /// =============================================================================================
        private void radioBtn1P_CheckedChanged(object sender, EventArgs e)
        {
            mDeviceIndex = 0;
        }

        /// =============================================================================================
        /// ==== radioBtn2P Click  ====
        /// =============================================================================================
        private void radioBtn2P_CheckedChanged(object sender, EventArgs e)
        {
            mDeviceIndex = 1;
        }

        /// =============================================================================================
        /// ==== Read  Mode Btn Click  ====
        /// =============================================================================================
        private void Btn_ReadMode_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            label_Ver.Text = "";
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            SendByteBuf[0] = (byte)UserCommand.GET_MODE;
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            byte[] InputReportBuf = new byte[64];
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex,DefaultOffset, ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            if ( InputReportBuf[0] == (byte)UserCommand.GET_MODE &&
                (InputReportBuf[2] == (byte)UserProID.ID_ADX || InputReportBuf[2] == (byte)UserProID.ID_ADX16 || InputReportBuf[2] == (byte)UserProID.ID_ADX_TP))  //V1.0.0.19:20241219(1)Support ADX 16Key and Touch Production ID
            {
                if ((InputReportBuf[1] & (byte)UserMode.MSEL_MAK) == (byte)UserMode.ADX_1PS)
                {
                    radioBtn1P.Checked = true;
                    radioBtn2P.Checked = false;
                    comboBox1.SelectedIndex = 0;    //1P
                    label_Ver.Text = "1P : ";
                }
                else
                {
                    radioBtn1P.Checked = false;
                    radioBtn2P.Checked = true;
                    comboBox1.SelectedIndex = 1;    //2P
                    label_Ver.Text = "2P : ";
                }

                if ((InputReportBuf[1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.ADX_KBS)
                {
                    comboBox2.SelectedIndex = 0;    //KBS
                    label_Ver.Text += "KBS, ";
                }
                else if ((InputReportBuf[1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.ADX_HID)
                {
                    comboBox2.SelectedIndex = 1;    //HID
                    label_Ver.Text += "HID, ";
                }
                else if ((InputReportBuf[1] & (byte)UserMode.MODE_MAK) == (byte)UserMode.ADX_IO4)
                {
                    comboBox2.SelectedIndex = 2;    //IO4
                    label_Ver.Text += "IO4, ";
                }

                label_Ver.Text += "UUID=" + InputReportBuf[2].ToString("X02") + ", " +
                 "HVer=" + InputReportBuf[3].ToString("X02") + ", " +
                 "FVer=" + InputReportBuf[4].ToString() + "." + InputReportBuf[5].ToString("D02");

            }
            //-------------------------------------------------------------------------------------------
            string RxStr = "GetInputReport:";
            for (int i = 0; i < ReportSize; i++)
            {
                RxStr += InputReportBuf[i].ToString("X02");
            }
            Console.WriteLine(RxStr);
            //Console.Write("GetInputReport:" + ex.Message);
            //-------------------------------------------------------------------------------------------
        }

				/// =============================================================================================
        /// ==== Write  Mode Btn Click  ====
        /// =============================================================================================
        private void Btn_WriteMode_Click(object sender, EventArgs e)//V1.0.0.18:20241012(1)Change ADX Save Issue
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            SendByteBuf[0] = (byte)UserCommand.SET_MODE;
            if (comboBox1.SelectedIndex == 0)
            {
                if (comboBox2.SelectedIndex == 0)
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_1PS | (byte)UserMode.ADX_KBS);
                }
                else if (comboBox2.SelectedIndex == 1)
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_1PS | (byte)UserMode.ADX_HID);
                }
                else
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_1PS | (byte)UserMode.ADX_IO4);
                }
            }
            else
            {
                if (comboBox2.SelectedIndex == 0)
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_2PS | (byte)UserMode.ADX_KBS);
                }
                else if (comboBox2.SelectedIndex == 1)
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_2PS | (byte)UserMode.ADX_HID);
                }
                else
                {
                    SendByteBuf[1] = ((byte)UserMode.ADX_2PS | (byte)UserMode.ADX_IO4);
                }
            }
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            Btn_ReadMode_Click(null, null);
            //-------------------------------------------------------------------------------------------
        }
        
        /// =============================================================================================
        /// ==== PictureBox Update ====
        /// =============================================================================================
        private void Btn_OTA_Click(object sender, EventArgs e)
        {
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        /// =============================================================================================
        /// ==== PictureBox Disable All ====
        /// =============================================================================================
        private void PictureBox_Disable_all()
        {
            pictureBox_A1.Visible = false;
            pictureBox_A2.Visible = false;
            pictureBox_A3.Visible = false;
            pictureBox_A4.Visible = false;
            pictureBox_A5.Visible = false;
            pictureBox_A6.Visible = false;
            pictureBox_A7.Visible = false;
            pictureBox_A8.Visible = false;

            pictureBox_B1.Visible = false;
            pictureBox_B2.Visible = false;
            pictureBox_B3.Visible = false;
            pictureBox_B4.Visible = false;
            pictureBox_B5.Visible = false;
            pictureBox_B6.Visible = false;
            pictureBox_B7.Visible = false;
            pictureBox_B8.Visible = false;

            pictureBox_C1.Visible = false;
            pictureBox_C2.Visible = false;

            pictureBox_D1.Visible = false;
            pictureBox_D2.Visible = false;
            pictureBox_D3.Visible = false;
            pictureBox_D4.Visible = false;
            pictureBox_D5.Visible = false;
            pictureBox_D6.Visible = false;
            pictureBox_D7.Visible = false;
            pictureBox_D8.Visible = false;

            pictureBox_E1.Visible = false;
            pictureBox_E2.Visible = false;
            pictureBox_E3.Visible = false;
            pictureBox_E4.Visible = false;
            pictureBox_E5.Visible = false;
            pictureBox_E6.Visible = false;
            pictureBox_E7.Visible = false;
            pictureBox_E8.Visible = false;

            pictureBox_BTN1.Visible = false;
            pictureBox_BTN2.Visible = false;
            pictureBox_BTN3.Visible = false;
            pictureBox_BTN4.Visible = false;
            pictureBox_BTN5.Visible = false;
            pictureBox_BTN6.Visible = false;
            pictureBox_BTN7.Visible = false;
            pictureBox_BTN8.Visible = false;

            pictureBox_BTN9.Visible = false;
            pictureBox_BTN10.Visible = false;
            pictureBox_BTN11.Visible = false;
        }

        /// =============================================================================================
        /// ==== button1 debug test ====
        /// =============================================================================================
        int debug_step = 0;
        private void button1_Click(object sender, EventArgs e)
        {
            //------------------------------------------------------------------------A1-A8
            if (debug_step == 0) { debug_step++; pictureBox_A1.Visible = true; }
            else if (debug_step == 1) { debug_step++; pictureBox_A2.Visible = true; }
            else if (debug_step == 2) { debug_step++; pictureBox_A3.Visible = true; }
            else if (debug_step == 3) { debug_step++; pictureBox_A4.Visible = true; }
            else if (debug_step == 4) { debug_step++; pictureBox_A5.Visible = true; }
            else if (debug_step == 5) { debug_step++; pictureBox_A6.Visible = true; }
            else if (debug_step == 6) { debug_step++; pictureBox_A7.Visible = true; }
            else if (debug_step == 7) { debug_step++; pictureBox_A8.Visible = true; }
            //------------------------------------------------------------------------B1-B8
            else if (debug_step == 8) { debug_step++; pictureBox_B1.Visible = true; }
            else if (debug_step == 9) { debug_step++; pictureBox_B2.Visible = true; }
            else if (debug_step == 10) { debug_step++; pictureBox_B3.Visible = true; }
            else if (debug_step == 11) { debug_step++; pictureBox_B4.Visible = true; }
            else if (debug_step == 12) { debug_step++; pictureBox_B5.Visible = true; }
            else if (debug_step == 13) { debug_step++; pictureBox_B6.Visible = true; }
            else if (debug_step == 14) { debug_step++; pictureBox_B7.Visible = true; }
            else if (debug_step == 15) { debug_step++; pictureBox_B8.Visible = true; }
            //------------------------------------------------------------------------C1-C2
            else if (debug_step == 16) { debug_step++; pictureBox_C1.Visible = true; }
            else if (debug_step == 17) { debug_step++; pictureBox_C2.Visible = true; }
            //------------------------------------------------------------------------D1-D8
            else if (debug_step == 18) { debug_step++; pictureBox_D1.Visible = true; }
            else if (debug_step == 19) { debug_step++; pictureBox_D2.Visible = true; }
            else if (debug_step == 20) { debug_step++; pictureBox_D3.Visible = true; }
            else if (debug_step == 21) { debug_step++; pictureBox_D4.Visible = true; }
            else if (debug_step == 22) { debug_step++; pictureBox_D5.Visible = true; }
            else if (debug_step == 23) { debug_step++; pictureBox_D6.Visible = true; }
            else if (debug_step == 24) { debug_step++; pictureBox_D7.Visible = true; }
            else if (debug_step == 25) { debug_step++; pictureBox_D8.Visible = true; }
            //------------------------------------------------------------------------E1-E8
            else if (debug_step == 26) { debug_step++; pictureBox_E1.Visible = true; }
            else if (debug_step == 27) { debug_step++; pictureBox_E2.Visible = true; }
            else if (debug_step == 28) { debug_step++; pictureBox_E3.Visible = true; }
            else if (debug_step == 29) { debug_step++; pictureBox_E4.Visible = true; }
            else if (debug_step == 30) { debug_step++; pictureBox_E5.Visible = true; }
            else if (debug_step == 31) { debug_step++; pictureBox_E6.Visible = true; }
            else if (debug_step == 32) { debug_step++; pictureBox_E7.Visible = true; }
            else if (debug_step == 33) { debug_step++; pictureBox_E8.Visible = true; }
            //------------------------------------------------------------------------END
            else { debug_step = 0; PictureBox_Disable_all(); }
            //------------------------------------------------------------------------
            //PictureBox_Update();
            //------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== button2 debug test ====
        /// =============================================================================================
        int Btn_step = 0;
        private void button2_Click(object sender, EventArgs e)
        {
            if (Btn_step == 0) { Btn_step++; pictureBox_BTN1.Visible = true; }
            else if (Btn_step == 1) { Btn_step++; pictureBox_BTN2.Visible = true; }
            else if (Btn_step == 2) { Btn_step++; pictureBox_BTN3.Visible = true; }
            else if (Btn_step == 3) { Btn_step++; pictureBox_BTN4.Visible = true; }
            else if (Btn_step == 4) { Btn_step++; pictureBox_BTN5.Visible = true; }
            else if (Btn_step == 5) { Btn_step++; pictureBox_BTN6.Visible = true; }
            else if (Btn_step == 6) { Btn_step++; pictureBox_BTN7.Visible = true; }
            else if (Btn_step == 7) { Btn_step++; pictureBox_BTN8.Visible = true; }
            else if (Btn_step == 8) { Btn_step++; pictureBox_BTN9.Visible = true; }
            else if (Btn_step == 9) { Btn_step++; pictureBox_BTN10.Visible = true; }
            else if (Btn_step == 10) { Btn_step++; pictureBox_BTN11.Visible = true; }
            else { Btn_step = 0; PictureBox_Disable_all(); }
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //V1.0.0.15:20240920(1)Change ADX TP Delay Config
        /// =============================================================================================
        /// ==== Touch Delay Config ====
        /// =============================================================================================      
        private void Btn_Delay_Config_Click(object sender, EventArgs e)
        {
            if (groupBox_Delay.Visible == false)
            {
                groupBox_Delay.Visible = true;

                textBox_Delay.Text = "0";
                if (bLanguageEN == true)
                {
                    Btn_Read_Delay.Text = "Read Delay";
                    Btn_Write_Delay.Text = "Save Delay";
                }
                else
                {
                    Btn_Read_Delay.Text = "读取延时";
                    Btn_Write_Delay.Text = "保存延时";
                }

                label_TPValue.Text = "";
                groupBox_Mode.Visible = false;
                label_Pkt_Count.Visible = false;

                pictureBox.Visible = false;
                panel1.Visible = false;
                panel2.Visible = false;

                btn_TouchTest.Enabled = false;
                btn_ButtonTest.Enabled = false;
                btn_default.Enabled = false;
                btn_Read.Enabled = false;
                btn_Save.Enabled = false;

                Btn_Sens_Open.Visible = false;//V1.0.0.23:20250322:(2)ADX Sens Save as and Import
                Btn_Sens_Save.Visible = false;

                Btn_Read_Delay_Click(null, null);

            }
            else
            {
                groupBox_Delay.Visible = false;

                label_TPValue.Text = "";
                groupBox_Mode.Visible = true;
                label_Pkt_Count.Visible = true;

                pictureBox.Visible = true;
                panel1.Visible = true;
                panel2.Visible = false;

                btn_TouchTest.Enabled = true;
                btn_ButtonTest.Enabled = true;
                btn_default.Enabled = true;
                btn_Read.Enabled = true;
                btn_Save.Enabled = true;

                Btn_Sens_Open.Visible = true;//V1.0.0.23:20250322:(2)ADX Sens Save as and Import
                Btn_Sens_Save.Visible = true;
            }
        }

        private void textBox_Delay_TextChanged(object sender, EventArgs e)
        {

        }

        private void Btn_Read_Delay_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            SendByteBuf[0] = (byte)UserCommand.GET_TPDS;
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            byte[] InputReportBuf = new byte[64];
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.GET_TPDS)
            {
                textBox_Delay.Text = InputReportBuf[1].ToString();
            }
            //-------------------------------------------------------------------------------------------
            string RxStr = "GetInputReport:";
            for (int i = 0; i < ReportSize; i++)
            {
                RxStr += InputReportBuf[i].ToString("X02");
            }
            Console.WriteLine(RxStr);
            //Console.Write("GetInputReport:" + ex.Message);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        private void Btn_Write_Delay_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            try
            {
                string InputStr = textBox_Delay.Text;
                if (InputStr.Length > 0)
                {
                    //-----------------------------------------------------------------------------------
                    int SensVal = Convert.ToInt32(InputStr);
                    //-----------------------------------------------------------------------------------
                    //byte index = 0;
                    byte ReportSize = 64;
                    byte[] SendByteBuf = new byte[64];
                    byte[] InputReportBuf = new byte[64];
                    //byte CS = 0x00;
                    //-----------------------------------------------------------------------------------
                    SendByteBuf[0] = (byte)UserCommand.SET_TPDS;
                    SendByteBuf[1] = (byte)SensVal;
                    //-----------------------------------------------------------------------------------
                    //V1.0.0.18:20241012(1)Change ADX Save Issue
                    //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
                    USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
                    //-----------------------------------------------------------------------------------
                    Thread.Sleep(100);
                    //-----------------------------------------------------------------------------------
                    //Btn_Read_Delay_Click(null, null);		//V1.0.0.18:20241012(1)Change ADX Save Issue
                    SendByteBuf[0] = (byte)UserCommand.GET_TPDS;
                    //V1.0.0.18:20241012(1)Change ADX Save Issue
                    //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
                    USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
                    //-------------------------------------------------------------------------------------------
                    Thread.Sleep(100);
                    //-------------------------------------------------------------------------------------------
                    //V1.0.0.18:20241012(1)Change ADX Save Issue
                    //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
                    USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);
                    //-------------------------------------------------------------------------------------------
                    if (InputReportBuf[0] == (byte)UserCommand.GET_TPDS)
                    {
                        textBox_Delay.Text = InputReportBuf[1].ToString();
                    }
                    //-------------------------------------------------------------------------------------------
                    string RxStr = "GetInputReport:";
                    for (int i = 0; i < ReportSize; i++)
                    {
                        RxStr += InputReportBuf[i].ToString("X02");
                    }
                    Console.WriteLine(RxStr);
                    //Console.Write("GetInputReport:" + ex.Message);
                    //-----------------------------------------------------------------------------------
                    MessageBox.Show("Write Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    //-----------------------------------------------------------------------------------
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            //-------------------------------------------------------------------------------------------
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        //V1.0.0.23:20250322:(2)ADX Sens Save as and Import
        /// =============================================================================================
        /// ==== Sens Save as and Import  ====
        /// =============================================================================================  
        private void Btn_Sens_Open_Click(object sender, EventArgs e)
        {
            string Currentpath = Directory.GetCurrentDirectory();

            Stream myStream = null;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = Currentpath;
            openFileDialog1.Filter = "cfg files (*.cfg)|*.cfg|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    //if ((myStream = openFileDialog1.OpenFile()) != null)
                    using (myStream = openFileDialog1.OpenFile())
                    {
                        Currentpath = openFileDialog1.FileName;
                        string Str = "";
                        using (StreamReader Rhandle = File.OpenText(Currentpath))         // Create a file to write to.
                        {
                            for (byte i = 0; i < 5; i++)
                            {
                                Str = Rhandle.ReadLine();
                                string[] S = Str.Split(':');
                                Str = S[0];
                                if (Str == "Sens")
                                {
                                    for (byte j = 0; j < 34; j++)
                                    {
                                        TP_SenBuffer[j] = Convert.ToInt32(S[j+1]);
                                    }

                                    btn_Save_Click(null, null); //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
        }

        private void Btn_Sens_Save_Click(object sender, EventArgs e)
        {
            string Currentpath = Directory.GetCurrentDirectory();
            string s = "";
            Stream myStream;
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.InitialDirectory = Currentpath;
            saveFileDialog1.Filter = "cfg files (*.cfg)|*.cfg|All files (*.*)|*.*";
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if ((myStream = saveFileDialog1.OpenFile()) != null)
                {
                    string FileName = saveFileDialog1.FileName;
                    myStream.Close();
                    using (StreamWriter Whandle = File.CreateText(FileName))         // Create a file to write to.
                    {
                        s = "Sens:";
                        for (byte i = 0; i < 34; i++)
                        {
                            s += TP_SenBuffer[i].ToString();
                            s += ":";
                        }
                        Whandle.WriteLine(s);
                    }
                }
            }
        }

        //V1.0.0.24:20250327(1)ADX Sens ADJ in Group All/A/B/C/D/E UI
        //V1.0.0.23:20250322:(1)ADX Sens ADJ in Group All/A/B/C/D/E
        /// =============================================================================================
        /// ==== Sens Group  ====
        /// ============================================================================================= 

        private void checkBox_All_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_All.Checked == true)
            {
                checkBox_A.Checked = true;
                checkBox_B.Checked = true;
                checkBox_C.Checked = true;
                checkBox_D.Checked = true;
                checkBox_E.Checked = true;
            }
            else
            {
                checkBox_A.Checked = false;
                checkBox_B.Checked = false;
                checkBox_C.Checked = false;
                checkBox_D.Checked = false;
                checkBox_E.Checked = false;
            }
        }

        private void checkBox_A_CheckedChanged(object sender, EventArgs e)
        {
            Display_Current_Sens((UInt32)ConfigIndex);
        }

        private void checkBox_B_CheckedChanged(object sender, EventArgs e)
        {
            Display_Current_Sens((UInt32)ConfigIndex);
        }

        private void checkBox_C_CheckedChanged(object sender, EventArgs e)
        {
            Display_Current_Sens((UInt32)ConfigIndex);
        }

        private void checkBox_D_CheckedChanged(object sender, EventArgs e)
        {
            Display_Current_Sens((UInt32)ConfigIndex);
        }

        private void checkBox_E_CheckedChanged(object sender, EventArgs e)
        {
            Display_Current_Sens((UInt32)ConfigIndex);
        }




        /// =============================================================================================
        /// ==== RGB Config  ====
        /// ============================================================================================= 
        /// V1.0.0.28:20250422(1)ADX Circle RGB Brightness and color set by App
        int Brightness = 255;
        int ColorRGB_R = 255;
        int ColorRGB_G = 255;
        int ColorRGB_B = 255;

        private void Btn_RGB_Read_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            SendByteBuf[0] = (byte)UserCommand.GET_RGBT;
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            byte[] InputReportBuf = new byte[64];
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[0] == (byte)UserCommand.GET_RGBT)
            {
                Brightness = InputReportBuf[1];
                ColorRGB_R = InputReportBuf[6];
                ColorRGB_G = InputReportBuf[7];
                ColorRGB_B = InputReportBuf[8];

                trackBar_RGB.Value = Brightness;
                double Percent = 0.0;
                Percent = (double)(Brightness);
                Percent *= 100.0;
                Percent /= (double)255;
                label_RGB.Text = "Brightness=" + Percent.ToString("F1") + "% (" + Brightness + ")";

                Btn_RGB_Sel.BackColor = Color.FromArgb(Brightness, ColorRGB_R, ColorRGB_G, ColorRGB_B);

                //Btn_RGB_Sel.BackColor = colorDialog1.Color;
                //ColorRGB_R = colorDialog1.Color.R;
                //ColorRGB_G = colorDialog1.Color.G;
                //ColorRGB_B = colorDialog1.Color.B;
            }
            //-------------------------------------------------------------------------------------------
            string RxStr = "GetInputReport:";
            for (int i = 0; i < ReportSize; i++)
            {
                RxStr += InputReportBuf[i].ToString("X02");
            }
            Console.WriteLine(RxStr);
            //Console.Write("GetInputReport:" + ex.Message);
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        private void Btn_RGB_Save_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            try
            {
                //-----------------------------------------------------------------------------------
                //byte index = 0;
                byte ReportSize = 64;
                byte[] SendByteBuf = new byte[64];
                byte[] InputReportBuf = new byte[64];
                //byte CS = 0x00;
                //-----------------------------------------------------------------------------------
                SendByteBuf[0] = (byte)UserCommand.SET_RGBT;
                SendByteBuf[1] = (byte)Brightness;
                SendByteBuf[2] = (byte)ColorRGB_R;
                SendByteBuf[3] = (byte)ColorRGB_G;
                SendByteBuf[4] = (byte)ColorRGB_B;
                //-----------------------------------------------------------------------------------
                //V1.0.0.18:20241012(1)Change ADX Save Issue
                //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
                USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
                //-----------------------------------------------------------------------------------
                Thread.Sleep(100);
                //-----------------------------------------------------------------------------------
                //Btn_Read_Delay_Click(null, null);		//V1.0.0.18:20241012(1)Change ADX Save Issue
                SendByteBuf[0] = (byte)UserCommand.GET_RGBT;
                //V1.0.0.18:20241012(1)Change ADX Save Issue
                //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
                USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
                //-------------------------------------------------------------------------------------------
                Thread.Sleep(100);
                //-------------------------------------------------------------------------------------------
                //V1.0.0.18:20241012(1)Change ADX Save Issue
                //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
                USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset, ReportInID, ReportSize, InputReportBuf);
                //-------------------------------------------------------------------------------------------
                if (InputReportBuf[0] == (byte)UserCommand.GET_RGBT)
                {
                    trackBar_RGB.Value = Brightness;
                    double Percent = 0.0;
                    Percent = (double)(Brightness);
                    Percent *= 100.0;
                    Percent /= (double)255;
                    label_RGB.Text = "Brightness=" + Percent.ToString("F1") + "% (" + Brightness + ")";
                    Btn_RGB_Sel.BackColor = Color.FromArgb(Brightness, ColorRGB_R, ColorRGB_G, ColorRGB_B);
                }
                //-------------------------------------------------------------------------------------------
                string RxStr = "GetInputReport:";
                for (int i = 0; i < ReportSize; i++)
                {
                    RxStr += InputReportBuf[i].ToString("X02");
                }
                Console.WriteLine(RxStr);
                //Console.Write("GetInputReport:" + ex.Message);
                //-----------------------------------------------------------------------------------
                MessageBox.Show("Write Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //-----------------------------------------------------------------------------------

            }
            catch (Exception ex)
            {
                MessageBox.Show("Input Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            //-------------------------------------------------------------------------------------------
        }

        private void Btn_RGB_Click(object sender, EventArgs e)
        {
            if (groupBox_RGB.Visible == true)
            {
                groupBox_RGB.Visible = false;
            }
            else
            {
                groupBox_RGB.Visible = true;
            }
        }

        private void Btn_RGB_Sel_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = colorDialog1.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                //pictureBox1.BackColor = colorDialog1.Color;
                Btn_RGB_Sel.BackColor = colorDialog1.Color;
                ColorRGB_R = colorDialog1.Color.R;
                ColorRGB_G = colorDialog1.Color.G;
                ColorRGB_B = colorDialog1.Color.B;
                //ValidColor = (UInt32)((colorDialog1.Color.R << 16) | (colorDialog1.Color.G << 8) | (colorDialog1.Color.B << 0));
                //label_ValidRGB.Text = ValidColor.ToString("X08");
                //button2.Text = colorDialog1.Color.R.ToString() + "-" + colorDialog1.Color.G.ToString() + "-" + colorDialog1.Color.B.ToString();
            }
        }

        private void trackBar_RGB_Scroll(object sender, EventArgs e)
        {
            int Val = trackBar_RGB.Value;
            double Percent = 0.0;
            Brightness = Val;
            Percent = (double)(Val);
            Percent *= 100.0;
            Percent /= (double)255;
            label_RGB.Text = "Brightness=" + Percent.ToString("F1") + "% (" + Val + ")";
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&


	}
}
