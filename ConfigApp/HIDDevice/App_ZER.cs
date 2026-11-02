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
    public partial class App_ZER : Form
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
        ///-------------------------------------------------------------------------------------------
        private string AppName = "Laverita Config";

				private byte mDeviceIndex = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportOutID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte ReportInID = 0;				//V1.0.0.18:20241012(1)Change ADX Save Issue
				private byte DefaultOffset = 0;			//V1.0.0.18:20241012(1)Change ADX Save Issue

                private UInt32 ValidColor = 0x0000FF00;          //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
                private UInt32 InValidColor = 0x000000FF;           //V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
        ///-------------------------------------------------------------------------------------------
        //System.IO.FileInfo[] files;
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

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
        //private bool bYesFouceStatusFG = false, bYesFouceStatusBKFG = false;
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

                    //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                    //----------------------------------------------------------------------------
                    if (KeyMode == 0x00)        //0x00 = 32Key,0x10 = 16Key,0x20 = 8Key,0x30 = 4Key
                    {
                        KeyMappingBuffer[KeyPadIndex] = kbh.vkCode;
                    }
                    else if (KeyMode == 0x10)
                    {
                        KeyMappingBuffer[(KeyPadIndex / 2) * 2 + 0] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 2) * 2 + 1] = kbh.vkCode;
                    }
                    else if (KeyMode == 0x20)
                    {
                        KeyMappingBuffer[(KeyPadIndex / 4) * 4 + 0] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 4) * 4 + 1] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 4) * 4 + 2] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 4) * 4 + 3] = kbh.vkCode;
                    }
                    else if (KeyMode == 0x30)
                    {
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 0] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 1] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 2] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 3] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 4] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 5] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 6] = kbh.vkCode;
                        KeyMappingBuffer[(KeyPadIndex / 8) * 8 + 7] = kbh.vkCode;
                    }
                    //----------------------------------------------------------------------------
                    App_UI_KeyPad_Display_KeyCode();
                    //textBox_vkCode.Text = "0x" + KeyMappingBuffer[KeyPadIndex].ToString("X02");
                    //textBox_Mapping.Text = vkCodeStringMapping[KeyMappingBuffer[KeyPadIndex]];

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
                    /*bStatus = USBCtrl.Connect();
                    if (bStatus == true)
                    {
                        if (bDeviceStatusFG == false)
                        {
                            bDeviceStatusFG = true;
                            DeivceFirstConnected();
                        }
                    }
                    else
                    {
                        if (bDeviceStatusFG == true)
                        {
                            bDeviceStatusFG = false;
                            DeivceFirstConnected();
                        }
                    }*/
                }
            }
            base.WndProc(ref m);
        }
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        /// =============================================================================================
        /// ====  ZER Init  ====
        /// =============================================================================================
        ///
        public App_ZER()
        {
            ///------------------------------------------------------------------------------------------
            InitializeComponent();
            ///------------------------------------------------------------------------------------------
            ReadAppConfig();
            ///------------------------------------------------------------------------------------------
             //V1.0.0.18:20241012(1)Change ADX Save Issue
            //bDeviceStatusFG = false;
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
            //if (USBCtrl.Connect() == true) bDeviceStatusFG = true;
            //DeivceFirstConnected();
             
            Find_USB_Deivce(); 
            ///------------------------------------------------------------------------------------------
            timer1.Enabled = true;
            timer1.Interval = 1;//ReadTagTime;//V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode   //V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
            ///------------------------------------------------------------------------------------------
            Load_Default_Setting();
            Load_Default_Mapping();
            //groupBox_Button.Visible = true;
            //groupBox_Sensi.Enabled = false;
            //groupBox_Color.Enabled = false;
            //groupBox_DTick.Enabled = false;

            ///------------------------------------------------------------------------------------------
            //tabControl1.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            //progressBar1.Direction = ProgressBarDirection.Vertical;
            //progressBar2.Direction = ProgressBarDirection.Vertical;
            //progressBar3.Direction = ProgressBarDirection.Vertical;
            //progressBar4.Direction = ProgressBarDirection.Vertical;
            ///------------------------------------------------------------------------------------------
            ///
            //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
            for (int i = 0; i < (MAX_TOUCH_NUM + MAX_IRSEN_NUM); i++)
            {
                KeyMappingBufBak[i] = 0;
            }
            ///------------------------------------------------------------------------------------------
            ///    
            App_UI_ReFlash();
            ///------------------------------------------------------------------------------------------
            ///
            //Btn_ReadMode_Click(null,null);
            ///------------------------------------------------------------------------------------------
        }
        /// =============================================================================================
        /// ==== Timer Tick ====
        /// =============================================================================================
        /// 
        //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
        int DeleStart = 0;
        int TickUI = 0;
        int TickRGB = 0;
        int TxFlag = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            ///------------------------------------------------------------------------------------------
            if (DeleStart < 1000)
            {
                DeleStart+=50;
                if (DeleStart == 1000)
                {
                    USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
                }
            }
            ///------------------------------------------------------------------------------------------
            /////V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
            TickUI++;
            if (TickUI >= 1)
            {
                TickUI = 0;
                MHidNeState = 0;
                for (int i = 0; i < 64; i++)
                {
                    if (HidValBAK[i] != HidValRAW[i])
                    {
                        HidValBAK[i] = HidValRAW[i];
                        MHidNeState = (byte)((i + 1));
                    }
                }

                if (bDebugModesFG == true)
                {
                    if (MHidNeState != 0)
                    {
                        if ((HidValBAK[0] & 0x01) != 0) Btn_IR6.Text = "ON";//1
                        else Btn_IR6.Text = "OFF";
                        if ((HidValBAK[0] & 0x02) != 0) Btn_IR1.Text = "ON";//2
                        else Btn_IR1.Text = "OFF";
                        if ((HidValBAK[0] & 0x04) != 0) Btn_IR5.Text = "ON";//3
                        else Btn_IR5.Text = "OFF";
                        if ((HidValBAK[0] & 0x08) != 0) Btn_IR2.Text = "ON";//4
                        else Btn_IR2.Text = "OFF";
                        if ((HidValBAK[0] & 0x10) != 0) Btn_IR4.Text = "ON";//5
                        else Btn_IR4.Text = "OFF";
                        if ((HidValBAK[0] & 0x20) != 0) Btn_IR3.Text = "ON";//6
                        else Btn_IR3.Text = "OFF";
                        if ((HidValBAK[0] & 0x40) != 0) Btn_IR7.Text = "ON";
                        else Btn_IR7.Text = "OFF";
                        if ((HidValBAK[0] & 0x80) != 0) Btn_IR8.Text = "ON";
                        else Btn_IR8.Text = "OFF";
                        
                        Btn_TP31.Text = HidValBAK[2].ToString();
                        Btn_TP32.Text = HidValBAK[3].ToString();
                        Btn_TP29.Text = HidValBAK[4].ToString();
                        Btn_TP30.Text = HidValBAK[5].ToString();
                        Btn_TP27.Text = HidValBAK[6].ToString();
                        Btn_TP28.Text = HidValBAK[7].ToString();
                        Btn_TP25.Text = HidValBAK[8].ToString();
                        Btn_TP26.Text = HidValBAK[9].ToString();
                        
                        Btn_TP23.Text = HidValBAK[10].ToString();
                        Btn_TP24.Text = HidValBAK[11].ToString();
                        Btn_TP21.Text = HidValBAK[12].ToString();
                        Btn_TP22.Text = HidValBAK[13].ToString();
                        Btn_TP19.Text = HidValBAK[14].ToString();
                        Btn_TP20.Text = HidValBAK[15].ToString();
                        Btn_TP17.Text = HidValBAK[16].ToString();
                        Btn_TP18.Text = HidValBAK[17].ToString();

                        Btn_TP15.Text = HidValBAK[18].ToString();
                        Btn_TP16.Text = HidValBAK[19].ToString();
                        Btn_TP13.Text = HidValBAK[20].ToString();
                        Btn_TP14.Text = HidValBAK[21].ToString();
                        Btn_TP11.Text = HidValBAK[22].ToString();
                        Btn_TP12.Text = HidValBAK[23].ToString();
                        Btn_TP9.Text = HidValBAK[24].ToString();
                        Btn_TP10.Text = HidValBAK[25].ToString();

                        Btn_TP7.Text = HidValBAK[26].ToString();
                        Btn_TP8.Text = HidValBAK[27].ToString();
                        Btn_TP5.Text = HidValBAK[28].ToString();
                        Btn_TP6.Text = HidValBAK[29].ToString();
                        Btn_TP3.Text = HidValBAK[30].ToString();
                        Btn_TP4.Text = HidValBAK[31].ToString();
                        Btn_TP1.Text = HidValBAK[32].ToString();
                        Btn_TP2.Text = HidValBAK[33].ToString();
                    }
                }
            }
            ///------------------------------------------------------------------------------------------
            //TickRGB++;
            ///------------------------------------------------------------------------------------------
        }

        /// =============================================
        /// ==== TestModeUI_Update====
        /// =============================================
        //V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
        Thread mUIUpDateThread;
        bool ThreadRunStatus;
        byte[] SendByteBuf = new byte[64];

        private void SetSendBuffer(int Index,int Point)
        {
            ///------------------------------------------------------------------------------------------
            if ((HidValBAK[Index] != 0) || (HidValBAK[Index + 1] != 0))
            {
                SendByteBuf[Point+0] = (byte)((ValidColor >> 16) & 0xff);
                SendByteBuf[Point+1] = (byte)((ValidColor >> 8) & 0xff);
                SendByteBuf[Point+2] = (byte)((ValidColor >> 0) & 0xff);
            }
            else
            {
                SendByteBuf[Point+0] = (byte)((InValidColor >> 16) & 0xff);
                SendByteBuf[Point+1] = (byte)((InValidColor >> 8) & 0xff);
                SendByteBuf[Point+2] = (byte)((InValidColor >> 0) & 0xff);
            }
            SendByteBuf[Point+3] = (byte)((ValidColor >> 16) & 0xff);
            SendByteBuf[Point+4] = (byte)((ValidColor >> 8) & 0xff);
            SendByteBuf[Point+5] = (byte)((ValidColor >> 0) & 0xff);
            ///------------------------------------------------------------------------------------------
        }

        private unsafe void UIUpDateThread_Running()//V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
        {
            ///------------------------------------------------------------------------------------------
            while (ThreadRunStatus==true)
            {
                ///------------------------------------------------------------------------------------------
                ///
                Thread.Sleep(5);
                ///------------------------------------------------------------------------------------------
                ///
                //if (TickRGB >= 1)
                {
                    TickRGB = 0;
                    int ReportSize = 64;
                    int Index = 0;
                    int Point = 0;
                    if (TxFlag != 0)
                    {
                        //---------------------------------------------------------------------------
                        TxFlag = 0;
                        Point = 1;
                        SendByteBuf[Point++] = (byte)TxFlag;
                        Index = 32; Point = 2;
                        //---------------------------------------------------------------------------1
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------2
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------3
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------4
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------5
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------6
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------7
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------8
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------9
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------10
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------
                    }
                    else
                    {
                        //---------------------------------------------------------------------------
                        TxFlag = 1;
                        Point = 1;
                        SendByteBuf[Point++] = (byte)TxFlag;
                        Index = 12; Point = 2;
                        //---------------------------------------------------------------------------1
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------2
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------3
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------4
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------5
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------6
                        SetSendBuffer(Index, Point); Index -= 2; Point += 6;
                        //---------------------------------------------------------------------------
                    }
                    //for (int i = 0; i < 10; i++)
                    /*{
                        SendByteBuf[Point++] = (byte)((InValidColor >> 16) & 0xff);
                        SendByteBuf[Point++] = (byte)((InValidColor >> 8) & 0xff);
                        SendByteBuf[Point++] = (byte)((InValidColor >> 0) & 0xff);
                        SendByteBuf[Point++] = (byte)((ValidColor >> 16) & 0xff);
                        SendByteBuf[Point++] = (byte)((ValidColor >> 8) & 0xff);
                        SendByteBuf[Point++] = (byte)((ValidColor >> 0) & 0xff);

                    }*/
                    //-------------------------------------------------------------------------------
                    USBCtrl.WriteReport_P1(SendByteBuf, ReportSize);
                    //Console.WriteLine("Zer HID" + mHIDLibraryState.Hid.Byte30 + mHIDLibraryState.Hid.Byte31 + mHIDLibraryState.Hid.Byte32 + mHIDLibraryState.Hid.Byte33);
                }
                ///--------------------------------------------------------------------------------------
                ///
            }
            ///------------------------------------------------------------------------------------------
        }

        private void UIUpDateThread_Start()//V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
        {
            ThreadRunStatus = true;
            mUIUpDateThread = new Thread(new ThreadStart(UIUpDateThread_Running));
            mUIUpDateThread.Start();
        }
        private void UIUpDateThread_Stop()//V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
        {
            ThreadRunStatus = false;
            mUIUpDateThread.Abort();
            mUIUpDateThread = null;
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
                                if (StrTab[1] == "EN")
                                {
                                    bLanguageEN = true;
                                    btn_default.Text = "DefaultSetting";
                                    btn_ReadSens.Text = "ReadSetting";
                                    btn_SaveSens.Text = "SaveSetting";
                                    checkBox_SetAll.Text = "All";
                                    Btn_TestMode.Text = "DebugMode";
                                    Btn_ReadMode.Text = "ReadMode";
                                    Btn_SaveMode.Text = "SaveMode";
                                    Btn_DefaultMap.Text = "DefaultSetting";
                                    Btn_MappingRead.Text = "ReadKeyMP";
                                    Btn_MappingMofy.Text = "ChangeKeyMP";
                                    Btn_MappingSave.Text = "SaveKeyMP";
                                    label_NewKey.Text = "NewKey:";
                                }
                                else
                                {
                                    bLanguageEN = false;
                                    btn_default.Text = "默认设置";
                                    btn_ReadSens.Text = "读取设置";
                                    btn_SaveSens.Text = "保存设置";
                                    checkBox_SetAll.Text = "全部";
                                    Btn_TestMode.Text = "调试模式";
                                    Btn_ReadMode.Text = "读取模式";
                                    Btn_SaveMode.Text = "保存模式";
                                    Btn_DefaultMap.Text = "默认设置";
                                    Btn_MappingRead.Text = "读取键值";
                                    Btn_MappingMofy.Text = "键值修改";
                                    Btn_MappingSave.Text = "保存键值";
                                    label_NewKey.Text = "新键值:";
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
        private void ZER_Form_Load(object sender, EventArgs e)
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
        private void ZER_Form_FormClosing(object sender, FormClosingEventArgs e)
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
        //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
        byte[] HidValRAW = new byte[64];
        byte[] HidValBAK = new byte[64];
        byte MHidNeState = 0;

        private void UpdateHidLibraryState(HIDLibraryLibChangedEventArgs args)
        {
            HIDLibraryState mHIDLibraryState = args.HIDLibraryState;
            switch (mHIDLibraryState.DeviceType)
            {
                case DeviceType.ZER_KBS_PID1: break;
                case DeviceType.ZER_HID_PID1:
                case DeviceType.ZER_HID_PID2:
                    //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
                    HidValRAW[0] = mHIDLibraryState.Hid.Byte0;
                    HidValRAW[1] = mHIDLibraryState.Hid.Byte1;
                    HidValRAW[2] = mHIDLibraryState.Hid.Byte2;
                    HidValRAW[3] = mHIDLibraryState.Hid.Byte3;
                    HidValRAW[4] = mHIDLibraryState.Hid.Byte4;
                    HidValRAW[5] = mHIDLibraryState.Hid.Byte5;
                    HidValRAW[6] = mHIDLibraryState.Hid.Byte6;
                    HidValRAW[7] = mHIDLibraryState.Hid.Byte7;
         
                    HidValRAW[8] = mHIDLibraryState.Hid.Byte8;
                    HidValRAW[9] = mHIDLibraryState.Hid.Byte9;
                    HidValRAW[10] = mHIDLibraryState.Hid.Byte10;
                    HidValRAW[11] = mHIDLibraryState.Hid.Byte11;
                    HidValRAW[12] = mHIDLibraryState.Hid.Byte12;
                    HidValRAW[13] = mHIDLibraryState.Hid.Byte13;
                    HidValRAW[14] = mHIDLibraryState.Hid.Byte14;
                    HidValRAW[15] = mHIDLibraryState.Hid.Byte15;

                    HidValRAW[16] = mHIDLibraryState.Hid.Byte16;
                    HidValRAW[17] = mHIDLibraryState.Hid.Byte17;
                    HidValRAW[18] = mHIDLibraryState.Hid.Byte18;
                    HidValRAW[19] = mHIDLibraryState.Hid.Byte19;
                    HidValRAW[20] = mHIDLibraryState.Hid.Byte20;
                    HidValRAW[21] = mHIDLibraryState.Hid.Byte21;
                    HidValRAW[22] = mHIDLibraryState.Hid.Byte22;
                    HidValRAW[23] = mHIDLibraryState.Hid.Byte23;

                    HidValRAW[24] = mHIDLibraryState.Hid.Byte24;
                    HidValRAW[25] = mHIDLibraryState.Hid.Byte25;
                    HidValRAW[26] = mHIDLibraryState.Hid.Byte26;
                    HidValRAW[27] = mHIDLibraryState.Hid.Byte27;
                    HidValRAW[28] = mHIDLibraryState.Hid.Byte28;
                    HidValRAW[29] = mHIDLibraryState.Hid.Byte29;
                    HidValRAW[30] = mHIDLibraryState.Hid.Byte30;
                    HidValRAW[31] = mHIDLibraryState.Hid.Byte31;

                    HidValRAW[32] = mHIDLibraryState.Hid.Byte32;
                    HidValRAW[33] = mHIDLibraryState.Hid.Byte33;
                    HidValRAW[34] = mHIDLibraryState.Hid.Byte34;
                    HidValRAW[35] = mHIDLibraryState.Hid.Byte35;
                break;
                default: break;
            }
        }
         
         /// =============================================================================================
        /// ==== Find USB Connected ====
        /// =============================================================================================
        ///
        private void Find_USB_Deivce()	//V1.0.0.18:20241012(1)Change ADX Save Issue
        {
            //-------------------------------------------------------------------------------------------
            //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;//V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
            //-------------------------------------------------------------------------------------------
            this.Text = AppName;
            label_Ver.Text = "";
            //groupBox_Mode.Enabled = true;     //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
            groupBox_USBMode.Enabled = false;   //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
            //radioBtn1P.Enabled = false;
            //radioBtn2P.Enabled = false;
            //radioBtn1P.Checked = false;
            //radioBtn2P.Checked = false;
            bDeviceStatusFG = false;
            //-------------------------------------------------------------------------------------------
            if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_KBS_VID1, (int)DeviceType.ZER_KBS_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn1P.Enabled = true;
                //radioBtn1P.Checked = true;
                mDeviceIndex = 0;
                ReportOutID = 0x00;
                ReportInID = 0x01;
                DefaultOffset = 0x00;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_HID_VID1, (int)DeviceType.ZER_HID_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn1P.Enabled = true;
                //radioBtn1P.Checked = true;
                mDeviceIndex = 0;
                ReportOutID = 0x01;
                ReportInID = 0x01;
                DefaultOffset = 0x00;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_IO4_VID1, (int)DeviceType.ZER_IO4_PID1, (int)DeviceType.None) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn1P.Enabled = true;
                //radioBtn1P.Checked = true;
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
            /*if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_KBS_VID2, (int)DeviceType.None, (int)DeviceType.ZER_KBS_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn2P.Enabled = true;
                //radioBtn2P.Checked = true;
                mDeviceIndex = 1;
                ReportOutID = 0x00;
                ReportInID = 0x00;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_HID_VID2, (int)DeviceType.None, (int)DeviceType.ZER_HID_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn2P.Enabled = true;
                //radioBtn2P.Checked = true;
                mDeviceIndex = 1;
                ReportOutID = 0x01;
                ReportInID = 0x01;
                DefaultOffset = 0x01;
            }
            else if ((USBCtrl.USBLib_Connect((int)DeviceType.ZER_IO4_VID2, (int)DeviceType.None, (int)DeviceType.ZER_IO4_PID2) != (int)DeviceType.None))
            {
                bDeviceStatusFG = true;
                //radioBtn2P.Enabled = true;
                //radioBtn2P.Checked = true;
                mDeviceIndex = 1;
                ReportOutID = 0x10;
                ReportInID = 0x01;
                DefaultOffset = 0x00;
            }*/
            //-------------------------------------------------------------------------------------------
            if (bDeviceStatusFG == true)
            {
                //---------------------------------------------------------------------------------------
                this.Text = AppName + " (Device Connected)";
                //groupBox_Mode.Enabled = true;//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                groupBox_USBMode.Enabled = true;   //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                //---------------------------------------------------------------------------------------
                Btn_ReadMode_Click(null, null);
                //---------------------------------------------------------------------------------------
                //USBCtrl.HIDLibraryLibChanged += HID_Event_Changed_Delegate;
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
        /// ==== Touch Screen   ====
        /// =============================================================================================
        /// 
        private int KeyPadIndex = 0;
        private byte KeyMode = 0;       //0x00 = 32Key,0x10 = 16Key,0x20 = 8Key,0x30 = 4Key
        private byte USBMode = 0;       //0x00 = Keyboard,0x01 = Hid,0x02 = IO4,
        private int[] TP_SenBuffer = new int[38];   //32+6  
        private int[] KeyMappingBuffer = new int[38];   //32+6  
        private int[] KeyMappingBufBak = new int[38];   //32+6  

        private bool bMappingModifFG = false;
        //private bool bTestRunningFG = false;


        private int MAX_TOUCH_NUM = 32;
        private int MAX_IRSEN_NUM = 6;
        private int SensMax = 3000;

        readonly int[] TP_SenDefault = new int[]    //32+6  //灵敏度设置: 0~1000, 数值越小越灵敏
        {
          300, //1
          300, //2
          300, //3
          300, //4
          300, //5
          300, //6
          300, //7
          300, //8
          300, //9
          300, //10
          300, //11
          300, //12
          300, //13
          300, //14
          300, //15
          300, //16
          300, //17
          300, //18
          300, //19
          300, //20
          300, //21
          300, //22
          300, //23
          300, //24
          300, //25
          300, //26
          300, //27
          300, //28
          300, //29
          300, //30
          300, //31
          300, //32

          300, //33
          300, //34
          300, //35
          300, //36
          300, //37
          300, //38
        };

        readonly int[] KeyCodeMappingDefault = new int[]//32+6
        {
          //'1','q','2','w',  '3','e','4','r', '5','t','6','y',  '7','u','8','i',
          //'a','z','s','x',  'd','c','f','v', 'g','b','h','n',  'j','m','k',',',

          //'/','.',',',';',  '[',']',

          0x31,0x51,0x32,0x57,0x33,0x45,0x34,0x52,0x35,0x54,0x36,0x59,0x37,0x55,0x38,0x49,
          0x41,0x5a,0x53,0x58,0x44,0x43,0x46,0x56,0x47,0x42,0x48,0x4e,0x4a,0x4d,0x4b,0xbc,

          0xbf,0xbe,0xbc,0xba,0xdb,0xdd
        };


        /// =============================================================================================
        /// ==== Load  Default Value ====
        /// =============================================================================================
        private void Load_Default_Setting()
        {
            for (int i = 0; i < MAX_TOUCH_NUM; i++)
            {
                TP_SenBuffer[i] = TP_SenDefault[i];
            }
        }
        private void Load_Default_Mapping()
        {
            for (int i = 0; i < (MAX_TOUCH_NUM + MAX_IRSEN_NUM); i++)
            {
                KeyMappingBuffer[i] = KeyCodeMappingDefault[i];
            }
        }
        /// =============================================================================================
        /// ==== Read Value Btn Click  ====
        ///CMD : 52 45 41 44 00
        ///RSP : 52 52 53 50 00
        ///Data: 03 20 03 20 03 20 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 
        ///CS  : 66	
        ///CMD : 52 45 41 44 01
        ///RSP : 52 52 53 50 01 
        ///Data: 03 20 03 20 03 20 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 0320 
        ///CS  : 5C
        /// =============================================================================================
        private void Read_TouchScreen_Sensi()
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64; // 61;//          //V1.0.0.22:20250306(1)Zer IO4 Mode Read Param Fail
            byte index = 0;
            byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_SENS);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            SendByteBuf[index++] = (byte)(0x00);    //Index: 0x00 = Read Seni offset 0-16,0x01 = Read Seni offset 17-33 
          	//V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            if (radioButton_IO4.Checked == true)    //V1.0.0.22:20250306(1)Zer IO4 Mode Read Param Fail
            {
                ReportIndex = 0x00;
            }
            else
            {
                ReportIndex = 0x01;
            }
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x52 &&
                InputReportBuf[ReportIndex + 2] == 0x53 &&
                InputReportBuf[ReportIndex + 3] == 0x50 &&
                InputReportBuf[ReportIndex + 4] == 0x00)
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
                byte DataLen = (byte)((MAX_TOUCH_NUM/2) * 2);
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = USBCtrl.UPDATE_CRC(ReportIndex, InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[ReportIndex + PktLen - 1])
                {
                    for (int i = 0; i < DataLen/2; i++)
                    {
                        TP_SenBuffer[i] = ((InputReportBuf[ReportIndex + SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[ReportIndex + SyncLen + (i * 2 + 1)]));
                    }
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
            SendByteBuf[index++] = (byte)(0x01);    //Index: 0x00 = Read Seni offset 0-16,0x01 = Read Seni offset 17-33 
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
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x52 &&
                InputReportBuf[ReportIndex + 2] == 0x53 &&
                InputReportBuf[ReportIndex + 3] == 0x50 &&
                InputReportBuf[ReportIndex + 4] == 0x01)
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
                byte DataLen = (byte)((MAX_TOUCH_NUM / 2) * 2);
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = USBCtrl.UPDATE_CRC(ReportIndex, InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[ReportIndex + PktLen - 1])
                {
                    for (int i = 0; i < DataLen / 2; i++)
                    {
                        TP_SenBuffer[DataLen / 2 + i] = ((InputReportBuf[ReportIndex + SyncLen + (i * 2 + 0)] << 8) | (InputReportBuf[ReportIndex + SyncLen + (i * 2 + 1)]));
                    }
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
            //Display_Image(0xFFFF);
            //trackBar_Val.Value = 0;
            //trackBar_Val.Enabled = false;
            //label_Val.Text = "";
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        private void Save_TouchScreen_Sensi()
        {
            //-------------------------------------------------------------------------------------------
            //Open_Serial_Port();
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            byte ReportSize = 64;
            byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_SENS);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            SendByteBuf[index++] = (byte)(0x00);    //Index: 0x00 = Read Seni offset 0-16,0x01 = Read Seni offset 17-33 
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < (MAX_TOUCH_NUM / 2); i++)
            {
                SendByteBuf[index++] = (byte)((TP_SenBuffer[i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((TP_SenBuffer[i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            CS = USBCtrl.UPDATE_CRC(0, SendByteBuf, index);
            SendByteBuf[index++] = CS;
            //-------------------------------------------------------------------------------------------
             //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
             USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            if (radioButton_IO4.Checked == true)    //V1.0.0.22:20250306(1)Zer IO4 Mode Read Param Fail
            {
                ReportIndex = 0x00;
            }
            else
            {
                ReportIndex = 0x01;
            }
            //-------------------------------------------------------------------------------------------
             //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
             USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B &&
                InputReportBuf[ReportIndex + 4] == 0x00)
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
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[ReportIndex + 3].ToString() + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            SendByteBuf[index++] = (byte)(0x01);    //Index: 0x00 = Read Seni offset 0-16,0x01 = Read Seni offset 17-33 
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < (MAX_TOUCH_NUM / 2); i++)
            {
                SendByteBuf[index++] = (byte)((TP_SenBuffer[(MAX_TOUCH_NUM / 2) + i] >> 8) & 0xff);
                SendByteBuf[index++] = (byte)((TP_SenBuffer[(MAX_TOUCH_NUM / 2) + i] >> 0) & 0xff);
            }
            //-------------------------------------------------------------------------------------------
            CS = USBCtrl.UPDATE_CRC(0, SendByteBuf, index);
            SendByteBuf[index++] = CS;
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
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_SENS &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B &&
                InputReportBuf[ReportIndex + 4] == 0x01)
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
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[ReportIndex + 3].ToString() + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            //Display_Image(0xFFFF);
            //trackBar_Val.Value = 0;
            //trackBar_Val.Enabled = false;
            //label_Val.Text = "";
            MessageBox.Show("Write Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Read Value Btn Click  ====
        ///CMD : 4E 45 41 44 00
        ///RSP : 4E 52 53 50 00
        ///Data: 61 62 63 64 65 66 67 68 69 6A 6B 6C 6D 6E 6F 70 71 72 73 74 75 76 77 78 79 7A 2C 2E 3B 3C 3E 3A 5B 5D 7B 7D 2F 2D 
        ///CS  : 66
        /// =============================================================================================
        private void Read_KeyMapping_Config()
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte index = 0;
            byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.GET_MAPP);
            SendByteBuf[index++] = (byte)(0x45);
            SendByteBuf[index++] = (byte)(0x41);
            SendByteBuf[index++] = (byte)(0x44);
            SendByteBuf[index++] = (byte)(0x00);    //Null
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
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.GET_MAPP &&
                InputReportBuf[ReportIndex + 1] == 0x52 &&
                InputReportBuf[ReportIndex + 2] == 0x53 &&
                InputReportBuf[ReportIndex + 3] == 0x50 &&
                InputReportBuf[ReportIndex + 4] == 0x00)
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
                byte DataLen = (byte)(MAX_TOUCH_NUM + MAX_IRSEN_NUM);
                byte CSlen = 1;
                byte PktLen = (byte)(SyncLen + DataLen + CSlen);
                byte CS = USBCtrl.UPDATE_CRC(ReportIndex, InputReportBuf, (byte)(SyncLen + DataLen));
                //---------------------------------------------------------------------------------------
                if (CS == InputReportBuf[ReportIndex + PktLen - 1])
                {
                    for (int i = 0; i < DataLen; i++)
                    {
                        KeyMappingBuffer[i] = (char)(InputReportBuf[ReportIndex + SyncLen + (i)]);
                    }
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
            //Display_Image(0xFFFF);
            //trackBar_Val.Value = 0;
            //trackBar_Val.Enabled = false;
            //label_Val.Text = "";
            MessageBox.Show("Read Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Save Value Btn Click  ====
        ///CMD : 57 52 49 54 00
        ///Data: 61 62 63 64 65 66 67 68 69 6A 6B 6C 6D 6E 6F 70 71 72 73 74 75 76 77 78 79 7A 2C 2E 3B 3C 3E 3A 5B 5D 7B 7D 2F 2D
        ///CS  : FA
        ///RSP : 57 4F 4B FA 00 
        /// =============================================================================================
        private void Save_KeyMapping_Config()
        {
            //-------------------------------------------------------------------------------------------
            //Open_Serial_Port();
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            byte ReportSize = 64;
            byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            byte[] InputReportBuf = new byte[64];
            byte CS = 0x00;
            //-------------------------------------------------------------------------------------------
            index = 0;
            SendByteBuf[index++] = (byte)(UserCommand.SET_MAPP);
            SendByteBuf[index++] = (byte)(0x52);
            SendByteBuf[index++] = (byte)(0x49);
            SendByteBuf[index++] = (byte)(0x54);
            SendByteBuf[index++] = (byte)(0x00);    //Index: 0x00 = Read Seni offset 0-16,0x01 = Read Seni offset 17-33 
            //-------------------------------------------------------------------------------------------
            for (int i = 0; i < (MAX_TOUCH_NUM + MAX_IRSEN_NUM); i++)
            {
                SendByteBuf[index++] = (byte)(KeyMappingBuffer[i]);
            }
            //-------------------------------------------------------------------------------------------
            CS = USBCtrl.UPDATE_CRC(0, SendByteBuf, index);
            SendByteBuf[index++] = CS;
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
            if (InputReportBuf[ReportIndex + 0] == (byte)UserCommand.SET_MAPP &&
                InputReportBuf[ReportIndex + 1] == 0x4F &&
                InputReportBuf[ReportIndex + 2] == 0x4B &&
                InputReportBuf[ReportIndex + 4] == 0x00)
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
                MessageBox.Show("Write Config Error !!! (" + InputReportBuf[ReportIndex + 3].ToString() + ")", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //-------------------------------------------------------------------------------------------
            //Display_Image(0xFFFF);
            //trackBar_Val.Value = 0;
            //trackBar_Val.Enabled = false;
            //label_Val.Text = "";
            MessageBox.Show("Write Complete !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Mode Switch  ====
        /// =============================================================================================
        private void App_Mode_Switch(byte NewMode)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            SendByteBuf[0] = (byte)UserCommand.SET_MODE;
            SendByteBuf[1] = (byte)NewMode;
            //-------------------------------------------------------------------------------------------
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
             USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== Read  Mode  ====
        /// =============================================================================================
        private byte App_Mode_Read()
        {
            //-------------------------------------------------------------------------------------------
            label_Ver.Text = "";
            //-------------------------------------------------------------------------------------------
            byte WorkMode = 0xff;
            byte ReportSize = 64;
            byte ReportIndex = 0x01;
            byte[] SendByteBuf = new byte[64];
            SendByteBuf[0] = (byte)UserCommand.GET_MODE;
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
            USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            Thread.Sleep(100);
            //-------------------------------------------------------------------------------------------
            if (radioButton_IO4.Checked == true)
            {
                ReportIndex = 0x00;
            }
            else
            {
                ReportIndex = 0x01;
            }
            //-------------------------------------------------------------------------------------------
            byte[] InputReportBuf = new byte[64];
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlInput(InputReportBuf, ReportSize);
             USBCtrl.USBLib_DeviceCtrlInput(mDeviceIndex, DefaultOffset,ReportInID, ReportSize, InputReportBuf);
            //-------------------------------------------------------------------------------------------
            if (InputReportBuf[ReportIndex+0] == (byte)UserCommand.GET_MODE)
            {
                WorkMode = InputReportBuf[ReportIndex+1];

                //V1.0.0.15:20240920(1)Change ADX TP Delay Config
                if (InputReportBuf[ReportIndex + 2] == (byte)UserProID.ID_ZER)
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
                RxStr += InputReportBuf[ReportIndex+i].ToString("X02");
            }
            Console.WriteLine(RxStr);
            //Console.Write("GetInputReport:" + ex.Message);
            //-------------------------------------------------------------------------------------------
            return WorkMode;
            //-------------------------------------------------------------------------------------------
        }

        /// =============================================================================================
        /// ==== OTA Switch ====
        /// =============================================================================================
        private void App_OTA_Switch()
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
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
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
            Btn_TP1.Text = "1";
            Btn_TP2.Text = "2";
            Btn_TP3.Text = "3";
            Btn_TP4.Text = "4";
            Btn_TP5.Text = "5";
            Btn_TP6.Text = "6";
            Btn_TP7.Text = "7";
            Btn_TP8.Text = "8";
            Btn_TP9.Text = "9";
            Btn_TP10.Text = "10";
            Btn_TP11.Text = "11";
            Btn_TP12.Text = "12";
            Btn_TP13.Text = "13";
            Btn_TP14.Text = "14";
            Btn_TP15.Text = "15";
            Btn_TP16.Text = "16";

            Btn_TP17.Text = "17";
            Btn_TP18.Text = "18";
            Btn_TP19.Text = "19";
            Btn_TP20.Text = "20";
            Btn_TP21.Text = "21";
            Btn_TP22.Text = "22";
            Btn_TP23.Text = "23";
            Btn_TP24.Text = "24";
            Btn_TP25.Text = "25";
            Btn_TP26.Text = "26";
            Btn_TP27.Text = "27";
            Btn_TP28.Text = "28";
            Btn_TP29.Text = "29";
            Btn_TP30.Text = "30";
            Btn_TP31.Text = "31";
            Btn_TP32.Text = "32";

            Btn_IR1.Text = "1";
            Btn_IR2.Text = "2";
            Btn_IR3.Text = "3";
            Btn_IR4.Text = "4";
            Btn_IR5.Text = "5";
            Btn_IR6.Text = "6";
        }
        /// =============================================================================================
        /// ==== UI Keypad Display ====
        /// =============================================================================================
        private void App_UI_KeyPad_Display_KeyCode()
        {
            Btn_TP1.Text = vkCodeStringMapping[KeyMappingBuffer[0]];
            Btn_TP2.Text = vkCodeStringMapping[KeyMappingBuffer[1]];
            Btn_TP3.Text = vkCodeStringMapping[KeyMappingBuffer[2]];
            Btn_TP4.Text = vkCodeStringMapping[KeyMappingBuffer[3]];
            Btn_TP5.Text = vkCodeStringMapping[KeyMappingBuffer[4]];
            Btn_TP6.Text = vkCodeStringMapping[KeyMappingBuffer[5]];
            Btn_TP7.Text = vkCodeStringMapping[KeyMappingBuffer[6]];
            Btn_TP8.Text = vkCodeStringMapping[KeyMappingBuffer[7]];
            Btn_TP9.Text = vkCodeStringMapping[KeyMappingBuffer[8]];
            Btn_TP10.Text = vkCodeStringMapping[KeyMappingBuffer[9]];
            Btn_TP11.Text = vkCodeStringMapping[KeyMappingBuffer[10]];
            Btn_TP12.Text = vkCodeStringMapping[KeyMappingBuffer[11]];
            Btn_TP13.Text = vkCodeStringMapping[KeyMappingBuffer[12]];
            Btn_TP14.Text = vkCodeStringMapping[KeyMappingBuffer[13]];
            Btn_TP15.Text = vkCodeStringMapping[KeyMappingBuffer[14]];
            Btn_TP16.Text = vkCodeStringMapping[KeyMappingBuffer[15]];

            Btn_TP17.Text = vkCodeStringMapping[KeyMappingBuffer[16]];
            Btn_TP18.Text = vkCodeStringMapping[KeyMappingBuffer[17]];
            Btn_TP19.Text = vkCodeStringMapping[KeyMappingBuffer[18]];
            Btn_TP20.Text = vkCodeStringMapping[KeyMappingBuffer[19]];
            Btn_TP21.Text = vkCodeStringMapping[KeyMappingBuffer[20]];
            Btn_TP22.Text = vkCodeStringMapping[KeyMappingBuffer[21]];
            Btn_TP23.Text = vkCodeStringMapping[KeyMappingBuffer[22]];
            Btn_TP24.Text = vkCodeStringMapping[KeyMappingBuffer[23]];
            Btn_TP25.Text = vkCodeStringMapping[KeyMappingBuffer[24]];
            Btn_TP26.Text = vkCodeStringMapping[KeyMappingBuffer[25]];
            Btn_TP27.Text = vkCodeStringMapping[KeyMappingBuffer[26]];
            Btn_TP28.Text = vkCodeStringMapping[KeyMappingBuffer[27]];
            Btn_TP29.Text = vkCodeStringMapping[KeyMappingBuffer[28]];
            Btn_TP30.Text = vkCodeStringMapping[KeyMappingBuffer[29]];
            Btn_TP31.Text = vkCodeStringMapping[KeyMappingBuffer[30]];
            Btn_TP32.Text = vkCodeStringMapping[KeyMappingBuffer[31]];

            Btn_IR1.Text = vkCodeStringMapping[KeyMappingBuffer[32]];
            Btn_IR2.Text = vkCodeStringMapping[KeyMappingBuffer[33]];
            Btn_IR3.Text = vkCodeStringMapping[KeyMappingBuffer[34]];
            Btn_IR4.Text = vkCodeStringMapping[KeyMappingBuffer[35]];
            Btn_IR5.Text = vkCodeStringMapping[KeyMappingBuffer[36]];
            Btn_IR6.Text = vkCodeStringMapping[KeyMappingBuffer[37]];

            textBox_vkCode.Text = "0x" + KeyMappingBuffer[KeyPadIndex].ToString("X02");
            textBox_Mapping.Text = vkCodeStringMapping[KeyMappingBuffer[KeyPadIndex]];
            //textBox_Mapping.Text = KeyMappingBuffer[KeyPadIndex].ToString();
        }
        
        /// =============================================================================================
        /// ==== UI Keypad Btn BackColor ====
        /// =============================================================================================
        private void App_UI_KeyPad_Btn_BackColor()
        {
            //----------------------------------------------------------------------------
            //if (bMappingModifFG == false)
            {
                if (KeyMode == 0x00)     //0x00 = 32Key,0x10 = 16Key,0x20 = 8Key,0x30 = 4Key
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BGNKey;
                }
                else if (KeyMode == 0x10)
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BG16Key;
                }
                else if (KeyMode == 0x20)
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BG8Key;
                }
                else if (KeyMode == 0x30)
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BG4Key;
                }
                /*
                if (KeyMode == 0x00)     //
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BGNKey;
                    Btn_TP1.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP2.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP3.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP4.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP5.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP6.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP7.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP8.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP9.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP10.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP11.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP12.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP13.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP14.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP15.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP16.BackColor = System.Drawing.Color.LightGray;

                    Btn_TP17.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP18.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP19.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP20.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP21.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP22.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP23.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP24.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP25.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP26.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP27.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP28.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP29.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP30.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP31.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP32.BackColor = System.Drawing.Color.LightGray;
                }
                else if (KeyMode == 0x01)
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BGNKey;
                    Btn_TP1.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP2.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP3.BackColor = System.Drawing.Color.DarkGray;
                    Btn_TP4.BackColor = System.Drawing.Color.DarkGray;
                    Btn_TP5.BackColor = System.Drawing.Color.DarkSalmon;
                    Btn_TP6.BackColor = System.Drawing.Color.DarkSalmon;
                    Btn_TP7.BackColor = System.Drawing.Color.LightCoral;
                    Btn_TP8.BackColor = System.Drawing.Color.LightCoral;

                    Btn_TP9.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP10.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP11.BackColor = System.Drawing.Color.OliveDrab;
                    Btn_TP12.BackColor = System.Drawing.Color.OliveDrab;
                    Btn_TP13.BackColor = System.Drawing.Color.PeachPuff;
                    Btn_TP14.BackColor = System.Drawing.Color.PeachPuff;
                    Btn_TP15.BackColor = System.Drawing.Color.SandyBrown;
                    Btn_TP16.BackColor = System.Drawing.Color.SandyBrown;

                    Btn_TP17.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP18.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP19.BackColor = System.Drawing.Color.Bisque;
                    Btn_TP20.BackColor = System.Drawing.Color.Bisque;
                    Btn_TP21.BackColor = System.Drawing.Color.LightGreen;
                    Btn_TP22.BackColor = System.Drawing.Color.LightGreen;
                    Btn_TP23.BackColor = System.Drawing.Color.Honeydew;
                    Btn_TP24.BackColor = System.Drawing.Color.Honeydew;

                    Btn_TP25.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP26.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP27.BackColor = System.Drawing.Color.LightBlue;
                    Btn_TP28.BackColor = System.Drawing.Color.LightBlue;
                    Btn_TP29.BackColor = System.Drawing.Color.Aquamarine;
                    Btn_TP30.BackColor = System.Drawing.Color.Aquamarine;
                    Btn_TP31.BackColor = System.Drawing.Color.PaleTurquoise;
                    Btn_TP32.BackColor = System.Drawing.Color.PaleTurquoise;
                }
                else if (KeyMode == 0x02)
                {
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BG8Key;
                    Btn_TP1.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP2.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP3.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP4.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP5.BackColor = System.Drawing.Color.DarkSalmon;
                    Btn_TP6.BackColor = System.Drawing.Color.DarkSalmon;
                    Btn_TP7.BackColor = System.Drawing.Color.DarkSalmon;
                    Btn_TP8.BackColor = System.Drawing.Color.DarkSalmon;

                    Btn_TP9.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP10.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP11.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP12.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP13.BackColor = System.Drawing.Color.PeachPuff;
                    Btn_TP14.BackColor = System.Drawing.Color.PeachPuff;
                    Btn_TP15.BackColor = System.Drawing.Color.PeachPuff;
                    Btn_TP16.BackColor = System.Drawing.Color.PeachPuff;

                    Btn_TP17.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP18.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP19.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP20.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP21.BackColor = System.Drawing.Color.LightGreen;
                    Btn_TP22.BackColor = System.Drawing.Color.LightGreen;
                    Btn_TP23.BackColor = System.Drawing.Color.LightGreen;
                    Btn_TP24.BackColor = System.Drawing.Color.LightGreen;

                    Btn_TP25.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP26.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP27.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP28.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP29.BackColor = System.Drawing.Color.Aquamarine;
                    Btn_TP30.BackColor = System.Drawing.Color.Aquamarine;
                    Btn_TP31.BackColor = System.Drawing.Color.Aquamarine;
                    Btn_TP32.BackColor = System.Drawing.Color.Aquamarine;
                }
                else if (KeyMode == 0x03)
                {
                    //myPictureBox.Image = global::ConfigAppNS.Properties.Resources.A1; 
                    pictureBox_TPKey_BG.BackgroundImage = ConfigAppNS.Properties.Resources.BG4Key;
                    Btn_TP1.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP2.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP3.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP4.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP5.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP6.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP7.BackColor = System.Drawing.Color.LightGray;
                    Btn_TP8.BackColor = System.Drawing.Color.LightGray;

                    Btn_TP9.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP10.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP11.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP12.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP13.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP14.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP15.BackColor = System.Drawing.Color.YellowGreen;
                    Btn_TP16.BackColor = System.Drawing.Color.YellowGreen;

                    Btn_TP17.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP18.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP19.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP20.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP21.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP22.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP23.BackColor = System.Drawing.Color.DarkOrange;
                    Btn_TP24.BackColor = System.Drawing.Color.DarkOrange;

                    Btn_TP25.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP26.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP27.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP28.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP29.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP30.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP31.BackColor = System.Drawing.Color.LightSkyBlue;
                    Btn_TP32.BackColor = System.Drawing.Color.LightSkyBlue;
                }*/

                Btn_TP1.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP2.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP3.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP4.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP5.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP6.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP7.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP8.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP9.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP10.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP11.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP12.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP13.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP14.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP15.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP16.BackColor = System.Drawing.SystemColors.ButtonFace;

                Btn_TP17.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP18.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP19.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP20.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP21.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP22.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP23.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP24.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP25.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP26.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP27.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP28.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP29.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP30.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP31.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP32.BackColor = System.Drawing.SystemColors.ButtonFace;

                Btn_IR1.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_IR2.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_IR3.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_IR4.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_IR5.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_IR6.BackColor = System.Drawing.SystemColors.ButtonFace;

                if (KeyPadIndex == 0) Btn_TP1.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 1) Btn_TP2.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 2) Btn_TP3.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 3) Btn_TP4.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 4) Btn_TP5.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 5) Btn_TP6.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 6) Btn_TP7.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 7) Btn_TP8.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 8) Btn_TP9.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 9) Btn_TP10.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 10) Btn_TP11.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 11) Btn_TP12.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 12) Btn_TP13.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 13) Btn_TP14.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 14) Btn_TP15.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 15) Btn_TP16.BackColor = System.Drawing.Color.IndianRed;

                else if (KeyPadIndex == 16) Btn_TP17.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 17) Btn_TP18.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 18) Btn_TP19.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 19) Btn_TP20.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 20) Btn_TP21.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 21) Btn_TP22.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 22) Btn_TP23.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 23) Btn_TP24.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 24) Btn_TP25.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 25) Btn_TP26.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 26) Btn_TP27.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 27) Btn_TP28.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 28) Btn_TP29.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 29) Btn_TP30.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 30) Btn_TP31.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 31) Btn_TP32.BackColor = System.Drawing.Color.IndianRed;

                else if (KeyPadIndex == 32) Btn_IR1.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 33) Btn_IR2.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 34) Btn_IR3.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 35) Btn_IR4.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 36) Btn_IR5.BackColor = System.Drawing.Color.IndianRed;
                else if (KeyPadIndex == 37) Btn_IR6.BackColor = System.Drawing.Color.IndianRed;

            }
            //----------------------------------------------------------------------------
        }


        /// =============================================================================================
        /// ==== UI ReFlash ====
        /// =============================================================================================
        private void App_UI_ReFlash()
        {
            //----------------------------------------------------------------------------
            if (bMappingModifFG == true)
            {
                textBox_vkCode.Text = "0x" + KeyMappingBuffer[KeyPadIndex].ToString("X02");
                textBox_Mapping.Text = vkCodeStringMapping[KeyMappingBuffer[KeyPadIndex]];
                //textBox_Mapping.Text = KeyMappingBuffer[KeyPadIndex].ToString();
            }
            //----------------------------------------------------------------------------
            App_UI_KeyPad_Btn_BackColor();
            //----------------------------------------------------------------------------
            if (KeyPadIndex < MAX_TOUCH_NUM)
            {
                int SensVal = TP_SenBuffer[KeyPadIndex];// trackBar_Sensi.Value;
                double Percent = (double)(100.0 * ((double)SensVal / SensMax));
                //TP_SenBuffer[KeyPadIndex] = SensVal;
                label_Sensi.Text = (KeyPadIndex + 1).ToString() + "=" + Percent.ToString("F1") + "% (" + SensVal + ")";
                trackBar_Sensi.Value = SensVal;
            }
            //----------------------------------------------------------------------------
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        private void Btn_TP1_Click(object sender, EventArgs e) { KeyPadIndex = 0; App_UI_ReFlash(); }
        private void Btn_TP2_Click(object sender, EventArgs e) { KeyPadIndex = 1; App_UI_ReFlash(); }
        private void Btn_TP3_Click(object sender, EventArgs e) { KeyPadIndex = 2; App_UI_ReFlash(); }
        private void Btn_TP4_Click(object sender, EventArgs e) { KeyPadIndex = 3; App_UI_ReFlash(); }
        private void Btn_TP5_Click(object sender, EventArgs e) { KeyPadIndex = 4; App_UI_ReFlash(); }
        private void Btn_TP6_Click(object sender, EventArgs e) { KeyPadIndex = 5; App_UI_ReFlash(); }
        private void Btn_TP7_Click(object sender, EventArgs e) { KeyPadIndex = 6; App_UI_ReFlash(); }
        private void Btn_TP8_Click(object sender, EventArgs e) { KeyPadIndex = 7; App_UI_ReFlash(); }
        private void Btn_TP9_Click(object sender, EventArgs e) { KeyPadIndex = 8; App_UI_ReFlash(); }
        private void Btn_TP10_Click(object sender, EventArgs e) { KeyPadIndex = 9; App_UI_ReFlash(); }
        private void Btn_TP11_Click(object sender, EventArgs e) { KeyPadIndex = 10; App_UI_ReFlash(); }
        private void Btn_TP12_Click(object sender, EventArgs e) { KeyPadIndex = 11; App_UI_ReFlash(); }
        private void Btn_TP13_Click(object sender, EventArgs e) { KeyPadIndex = 12; App_UI_ReFlash(); }
        private void Btn_TP14_Click(object sender, EventArgs e) { KeyPadIndex = 13; App_UI_ReFlash(); }
        private void Btn_TP15_Click(object sender, EventArgs e) { KeyPadIndex = 14; App_UI_ReFlash(); }
        private void Btn_TP16_Click(object sender, EventArgs e) { KeyPadIndex = 15; App_UI_ReFlash(); }
        private void Btn_TP17_Click(object sender, EventArgs e) { KeyPadIndex = 16; App_UI_ReFlash(); }
        private void Btn_TP18_Click(object sender, EventArgs e) { KeyPadIndex = 17; App_UI_ReFlash(); }
        private void Btn_TP19_Click(object sender, EventArgs e) { KeyPadIndex = 18; App_UI_ReFlash(); }
        private void Btn_TP20_Click(object sender, EventArgs e) { KeyPadIndex = 19; App_UI_ReFlash(); }
        private void Btn_TP21_Click(object sender, EventArgs e) { KeyPadIndex = 20; App_UI_ReFlash(); }
        private void Btn_TP22_Click(object sender, EventArgs e) { KeyPadIndex = 21; App_UI_ReFlash(); }
        private void Btn_TP23_Click(object sender, EventArgs e) { KeyPadIndex = 22; App_UI_ReFlash(); }
        private void Btn_TP24_Click(object sender, EventArgs e) { KeyPadIndex = 23; App_UI_ReFlash(); }
        private void Btn_TP25_Click(object sender, EventArgs e) { KeyPadIndex = 24; App_UI_ReFlash(); }
        private void Btn_TP26_Click(object sender, EventArgs e) { KeyPadIndex = 25; App_UI_ReFlash(); }
        private void Btn_TP27_Click(object sender, EventArgs e) { KeyPadIndex = 26; App_UI_ReFlash(); }
        private void Btn_TP28_Click(object sender, EventArgs e) { KeyPadIndex = 27; App_UI_ReFlash(); }
        private void Btn_TP29_Click(object sender, EventArgs e) { KeyPadIndex = 28; App_UI_ReFlash(); }
        private void Btn_TP30_Click(object sender, EventArgs e) { KeyPadIndex = 29; App_UI_ReFlash(); }
        private void Btn_TP31_Click(object sender, EventArgs e) { KeyPadIndex = 30; App_UI_ReFlash(); }
        private void Btn_TP32_Click(object sender, EventArgs e) { KeyPadIndex = 31; App_UI_ReFlash(); }

        private void Btn_IR1_Click(object sender, EventArgs e) { KeyPadIndex = 32; App_UI_ReFlash(); }
        private void Btn_IR2_Click(object sender, EventArgs e) { KeyPadIndex = 33; App_UI_ReFlash(); }
        private void Btn_IR3_Click(object sender, EventArgs e) { KeyPadIndex = 34; App_UI_ReFlash(); }
        private void Btn_IR4_Click(object sender, EventArgs e) { KeyPadIndex = 35; App_UI_ReFlash(); }
        private void Btn_IR5_Click(object sender, EventArgs e) { KeyPadIndex = 36; App_UI_ReFlash(); }
        private void Btn_IR6_Click(object sender, EventArgs e) { KeyPadIndex = 37; App_UI_ReFlash(); }

        private void trackBar_Sensi_Scroll(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            if (KeyPadIndex < MAX_TOUCH_NUM)
            {
                int SensVal = trackBar_Sensi.Value;
                double Percent = (double)(100.0 * ((double)SensVal / SensMax));

                if (checkBox_SetAll.Checked == false)
                {
                    TP_SenBuffer[KeyPadIndex] = SensVal;
                }
                else
                {
                    for (int i = 0; i < MAX_TOUCH_NUM; i++)
                    {
                        TP_SenBuffer[i] = SensVal;
                    }
                }

                label_Sensi.Text = (KeyPadIndex+1).ToString() + "=" + Percent.ToString("F1") + "% (" + SensVal + ")";
            }
            //----------------------------------------------------------------------------
        }

        private void btn_default_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            Load_Default_Setting();
            Load_Default_Mapping();
            //----------------------------------------------------------------------------
            radioButton_Key32.Checked = true;   //KeyMode = 0x00;
            radioButton_KBS.Checked = true;     //USBMode = 0x00;
            //----------------------------------------------------------------------------
            App_UI_ReFlash();
            //----------------------------------------------------------------------------
        }

        private void btn_ReadSens_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            Read_TouchScreen_Sensi();
            //----------------------------------------------------------------------------
            App_UI_ReFlash();
            //----------------------------------------------------------------------------
        }

        private void btn_SaveSens_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            Save_TouchScreen_Sensi();
            //----------------------------------------------------------------------------
        }

        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        private void Btn_MappingMofy_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            if (bMappingModifFG == false)
            {
                bMappingModifFG = true;
                groupBox_Sensi.Enabled = false;
                //groupBox_Mode.Enabled = true;// false;//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                groupBox_KeyMode.Enabled = true;   //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                textBox_vkCode.Enabled = true;
                textBox_Mapping.Enabled = true;
                Btn_DefaultMap.Enabled = true;
                Btn_MappingRead.Enabled = true;
                Btn_MappingSave.Enabled = true;
                groupBox_IRSensi.Enabled = true;
                label_NewKey.Enabled = true;
                //Read_KeyMapping_Config();
                App_UI_KeyPad_Display_KeyCode();
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
                groupBox_Sensi.Enabled = true;
                //groupBox_Mode.Enabled = false;//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                groupBox_KeyMode.Enabled = false;   //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
                textBox_vkCode.Enabled = false;
                textBox_Mapping.Enabled = false;
                Btn_DefaultMap.Enabled = false;
                Btn_MappingRead.Enabled = false;
                Btn_MappingSave.Enabled = false;
                groupBox_IRSensi.Enabled = false;
                label_NewKey.Enabled = false;
                App_UI_KeyPad_Display_IndexNum();
                Hook_Clear();

                if (bLanguageEN == true)
                {
                    Btn_MappingMofy.Text = "ChangeKeyMP";
                }
                else
                {
                    Btn_MappingMofy.Text = "键值修改";
                }
            }
            //----------------------------------------------------------------------------
        }
        private void textBox_Mapping_TextChanged(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            /*if (bMappingModifFG == true)
            {
                string Str = textBox_Mapping.Text;
                if (Str.Length == 1)
                {
                    char[] chars = Str.ToCharArray();
                    KeyMappingBuffer[KeyPadIndex] = chars[0];
                    App_UI_KeyPad_Display_KeyCode();
                }
            }*/
            //----------------------------------------------------------------------------
        }

        private void Btn_MappingRead_Click(object sender, EventArgs e)
        {
            Read_KeyMapping_Config();
            App_UI_KeyPad_Display_KeyCode();
        }

        private void Btn_DefaultMap_Click(object sender, EventArgs e)
        {
            Load_Default_Mapping();
            App_UI_KeyPad_Display_KeyCode();
        }

        private void Btn_MappingSave_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            Save_KeyMapping_Config();
            //----------------------------------------------------------------------------
        }
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

        //V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
        private void ReLoad_Default_KeyMap()
        {
            //----------------------------------------------------------------------------
            if (USBMode != 0) return;               //USBMode:0x00 = Keyboard,0x01 = Hid,0x02 = IO4
            //----------------------------------------------------------------------------
            /*bool bFirstChangeFG = true;
            for (int i = 0; i < (MAX_TOUCH_NUM); i++)
            {
                if (KeyMappingBufBak[i] != 0)
                {
                    bFirstChangeFG = false;
                    break;
                }
            }
            if (bFirstChangeFG == true)
            {
                for (int i = 0; i < (MAX_TOUCH_NUM); i++)
                {
                    KeyMappingBufBak[i] = KeyMappingBuffer[i];
                }
            }*/

            /*
            readonly int[] KeyCodeMappingDefault = new int[]//32+6
            {
          //'1','q','2','w',  '3','e','4','r', '5','t','6','y',  '7','u','8','i',
          //'a','z','s','x',  'd','c','f','v', 'g','b','h','n',  'j','m','k',',',

          //'/','.',',',';',  '[',']',

          0x31,0x51,0x32,0x57,0x33,0x45,0x34,0x52,0x35,0x54,0x36,0x59,0x37,0x55,0x38,0x49,
          0x41,0x5a,0x53,0x58,0x44,0x43,0x46,0x56,0x47,0x42,0x48,0x4e,0x4a,0x4d,0x4b,0xbc,

          0xbf,0xbe,0xbc,0xba,0xdb,0xdd
            }; 
  
             */
            //----------------------------------------------------------------------------
            for (int i = 0; i < (MAX_TOUCH_NUM); i++)
            {
                KeyMappingBuffer[i] = KeyCodeMappingDefault[i];
            }
            //for (int i = 0; i < (MAX_TOUCH_NUM + MAX_IRSEN_NUM); i++)
            //{
            //    KeyMappingBuffer[i] = KeyCodeMappingDefault[i];
            //}
            //----------------------------------------------------------------------------
            if (KeyMode == 0x00)        //0x00 = 32Key,0x10 = 16Key,0x20 = 8Key,0x30 = 4Key
            {
                //for (int i = 0; i < (MAX_TOUCH_NUM); i++)
                //{
                //    KeyMappingBuffer[i] = KeyMappingBufBak[i];
                //}
            }
            else if (KeyMode == 0x10)
            {
                KeyMappingBuffer[1] = KeyMappingBuffer[0];
                KeyMappingBuffer[3] = KeyMappingBuffer[2];
                KeyMappingBuffer[5] = KeyMappingBuffer[4];
                KeyMappingBuffer[7] = KeyMappingBuffer[6];

                KeyMappingBuffer[9] = KeyMappingBuffer[8];
                KeyMappingBuffer[11] = KeyMappingBuffer[10];
                KeyMappingBuffer[13] = KeyMappingBuffer[12];
                KeyMappingBuffer[15] = KeyMappingBuffer[14];

                KeyMappingBuffer[17] = KeyMappingBuffer[16];
                KeyMappingBuffer[19] = KeyMappingBuffer[18];
                KeyMappingBuffer[21] = KeyMappingBuffer[20];
                KeyMappingBuffer[23] = KeyMappingBuffer[22];

                KeyMappingBuffer[25] = KeyMappingBuffer[24];
                KeyMappingBuffer[27] = KeyMappingBuffer[26];
                KeyMappingBuffer[29] = KeyMappingBuffer[28];
                KeyMappingBuffer[31] = KeyMappingBuffer[30];
            }
            else if (KeyMode == 0x20)
            {
                KeyMappingBuffer[0] = KeyMappingBuffer[0];
                KeyMappingBuffer[1] = KeyMappingBuffer[0];
                KeyMappingBuffer[2] = KeyMappingBuffer[0];
                KeyMappingBuffer[3] = KeyMappingBuffer[0];
                KeyMappingBuffer[4] = KeyMappingBuffer[4];
                KeyMappingBuffer[5] = KeyMappingBuffer[4];
                KeyMappingBuffer[6] = KeyMappingBuffer[4];
                KeyMappingBuffer[7] = KeyMappingBuffer[4];

                KeyMappingBuffer[8] = KeyMappingBuffer[8];
                KeyMappingBuffer[9] = KeyMappingBuffer[8];
                KeyMappingBuffer[10] = KeyMappingBuffer[8];
                KeyMappingBuffer[11] = KeyMappingBuffer[8];
                KeyMappingBuffer[12] = KeyMappingBuffer[12];
                KeyMappingBuffer[13] = KeyMappingBuffer[12];
                KeyMappingBuffer[14] = KeyMappingBuffer[12];
                KeyMappingBuffer[15] = KeyMappingBuffer[12];

                KeyMappingBuffer[16] = KeyMappingBuffer[16];
                KeyMappingBuffer[17] = KeyMappingBuffer[16];
                KeyMappingBuffer[18] = KeyMappingBuffer[16];
                KeyMappingBuffer[19] = KeyMappingBuffer[16];
                KeyMappingBuffer[20] = KeyMappingBuffer[20];
                KeyMappingBuffer[21] = KeyMappingBuffer[20];
                KeyMappingBuffer[22] = KeyMappingBuffer[20];
                KeyMappingBuffer[23] = KeyMappingBuffer[20];

                KeyMappingBuffer[24] = KeyMappingBuffer[24];
                KeyMappingBuffer[25] = KeyMappingBuffer[24];
                KeyMappingBuffer[26] = KeyMappingBuffer[24];
                KeyMappingBuffer[27] = KeyMappingBuffer[24];
                KeyMappingBuffer[28] = KeyMappingBuffer[28];
                KeyMappingBuffer[29] = KeyMappingBuffer[28];
                KeyMappingBuffer[30] = KeyMappingBuffer[28];
                KeyMappingBuffer[31] = KeyMappingBuffer[28];
            }
            else if (KeyMode == 0x30)
            {
                KeyMappingBuffer[0] = KeyMappingBuffer[0];
                KeyMappingBuffer[1] = KeyMappingBuffer[0];
                KeyMappingBuffer[2] = KeyMappingBuffer[0];
                KeyMappingBuffer[3] = KeyMappingBuffer[0];
                KeyMappingBuffer[4] = KeyMappingBuffer[0];
                KeyMappingBuffer[5] = KeyMappingBuffer[0];
                KeyMappingBuffer[6] = KeyMappingBuffer[0];
                KeyMappingBuffer[7] = KeyMappingBuffer[0];

                KeyMappingBuffer[8] = KeyMappingBuffer[8];
                KeyMappingBuffer[9] = KeyMappingBuffer[8];
                KeyMappingBuffer[10] = KeyMappingBuffer[8];
                KeyMappingBuffer[11] = KeyMappingBuffer[8];
                KeyMappingBuffer[12] = KeyMappingBuffer[8];
                KeyMappingBuffer[13] = KeyMappingBuffer[8];
                KeyMappingBuffer[14] = KeyMappingBuffer[8];
                KeyMappingBuffer[15] = KeyMappingBuffer[8];

                KeyMappingBuffer[16] = KeyMappingBuffer[16];
                KeyMappingBuffer[17] = KeyMappingBuffer[16];
                KeyMappingBuffer[18] = KeyMappingBuffer[16];
                KeyMappingBuffer[19] = KeyMappingBuffer[16];
                KeyMappingBuffer[20] = KeyMappingBuffer[16];
                KeyMappingBuffer[21] = KeyMappingBuffer[16];
                KeyMappingBuffer[22] = KeyMappingBuffer[16];
                KeyMappingBuffer[23] = KeyMappingBuffer[16];

                KeyMappingBuffer[24] = KeyMappingBuffer[24];
                KeyMappingBuffer[25] = KeyMappingBuffer[24];
                KeyMappingBuffer[26] = KeyMappingBuffer[24];
                KeyMappingBuffer[27] = KeyMappingBuffer[24];
                KeyMappingBuffer[28] = KeyMappingBuffer[24];
                KeyMappingBuffer[29] = KeyMappingBuffer[24];
                KeyMappingBuffer[30] = KeyMappingBuffer[24];
                KeyMappingBuffer[31] = KeyMappingBuffer[24];
            }

            //----------------------------------------------------------------------------
            if (bMappingModifFG == true)
            {
                App_UI_KeyPad_Display_KeyCode();
            }
            //----------------------------------------------------------------------------
            else
            {
                //App_UI_KeyPad_Display_IndexNum();
            }
            //----------------------------------------------------------------------------
        }

        //KeyMode:0x00 = 32Key,0x10 = 16Key,0x20 = 8Key,0x30 = 4Key
        private void radioButton_Key32_CheckedChanged(object sender, EventArgs e) { KeyMode = 0x00; ReLoad_Default_KeyMap(); App_UI_ReFlash(); }//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
        private void radioButton_Key16_CheckedChanged(object sender, EventArgs e) { KeyMode = 0x10; ReLoad_Default_KeyMap(); App_UI_ReFlash(); }//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
        private void radioButton_Key8_CheckedChanged(object sender, EventArgs e) { KeyMode = 0x20; ReLoad_Default_KeyMap(); App_UI_ReFlash(); }//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)
        private void radioButton_Key4_CheckedChanged(object sender, EventArgs e) { KeyMode = 0x30; ReLoad_Default_KeyMap(); App_UI_ReFlash(); }//V1.0.0.31:20250522(1)KeyBoard Mode KeyMode(32/16/8/4)

        //USBMode:0x00 = Keyboard,0x01 = Hid,0x02 = IO4
        private void radioButton_KBS_CheckedChanged(object sender, EventArgs e) { USBMode = 0x00; App_UI_ReFlash(); }
        private void radioButton_HID_CheckedChanged(object sender, EventArgs e) { USBMode = 0x01; App_UI_ReFlash(); }
        private void radioButton_IO4_CheckedChanged(object sender, EventArgs e) { USBMode = 0x02; App_UI_ReFlash(); }

        private void Btn_ReadMode_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            byte WorkMode = 0x00;
            //----------------------------------------------------------------------------
            WorkMode = App_Mode_Read();
            //----------------------------------------------------------------------------
            if ((WorkMode & 0xf0) == 0x00)      //32Keys
            {
                radioButton_Key32.Checked = true;
                radioButton_Key16.Checked = false;
                radioButton_Key8.Checked = false;
                radioButton_Key4.Checked = false;
            }
            else if ((WorkMode & 0xf0) == 0x10) //16Keys
            {
                radioButton_Key32.Checked = false;
                radioButton_Key16.Checked = true;
                radioButton_Key8.Checked = false;
                radioButton_Key4.Checked = false;
            }
            else if ((WorkMode & 0xf0) == 0x20) //8Keys
            {
                radioButton_Key32.Checked = false;
                radioButton_Key16.Checked = false;
                radioButton_Key8.Checked = true;
                radioButton_Key4.Checked = false;
            }
            else if ((WorkMode & 0xf0) == 0x30) //4Keys
            {
                radioButton_Key32.Checked = false;
                radioButton_Key16.Checked = false;
                radioButton_Key8.Checked = false;
                radioButton_Key4.Checked = true;
            }
            //----------------------------------------------------------------------------
            if ((WorkMode & 0x0f) == 0x00)      //KeyBoard
            {
                radioButton_KBS.Checked = true;
                radioButton_HID.Checked = false;
                radioButton_IO4.Checked = false;
            }
            else if ((WorkMode & 0x0f) == 0x01) //HID
            {
                radioButton_KBS.Checked = false;
                radioButton_HID.Checked = true;
                radioButton_IO4.Checked = false;
            }
            else if ((WorkMode & 0x0f) == 0x02) //IO4
            {
                radioButton_KBS.Checked = false;
                radioButton_HID.Checked = false;
                radioButton_IO4.Checked = true;
            }
            //----------------------------------------------------------------------------
        }

        private void Btn_SaveMode_Click(object sender, EventArgs e)
        {
            //----------------------------------------------------------------------------
            App_Mode_Switch((byte)(KeyMode | USBMode));
            //----------------------------------------------------------------------------
        }

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
            //V1.0.0.18:20241012(1)Change ADX Save Issue
            //USBCtrl.USBDeviceCtrlOutput(SendByteBuf, ReportSize);
             USBCtrl.USBLib_DeviceCtrlOutput(mDeviceIndex, ReportOutID, ReportSize, SendByteBuf);
            //-------------------------------------------------------------------------------------------
            //App_OTA ota = new App_OTA();
            //this.Hide();
            //ota.Show();
            //-------------------------------------------------------------------------------------------
        }

        //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
        private void Btn_TestMode_Click(object sender, EventArgs e)
        {
            //-------------------------------------------------------------------------------------------
            byte ReportSize = 64;
            byte ReportID = 0;
            byte[] SendByteBuf = new byte[64];
            //-------------------------------------------------------------------------------------------
            byte index = 0;
            if (bDebugModesFG == false)
            {
                groupBox_Color.Visible = true;

                Btn_TP1.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP2.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP3.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP4.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP5.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP6.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP7.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP8.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP9.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP10.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP11.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP12.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP13.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP14.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP15.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP16.BackColor = System.Drawing.SystemColors.ButtonFace;

                Btn_TP17.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP18.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP19.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP20.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP21.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP22.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP23.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP24.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP25.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP26.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP27.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP28.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP29.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP30.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP31.BackColor = System.Drawing.SystemColors.ButtonFace;
                Btn_TP32.BackColor = System.Drawing.SystemColors.ButtonFace;

                Btn_TP1.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP2.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP3.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP4.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP5.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP6.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP7.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP8.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP9.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP10.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP11.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP12.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP13.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP14.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP15.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP16.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP17.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP18.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP19.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP20.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP21.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP22.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP23.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP24.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP25.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP26.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP27.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP28.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP29.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP30.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP31.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP32.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_IR1.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR2.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR3.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR4.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR5.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR6.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR7.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR8.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                bDebugModesFG = true;
                if (bLanguageEN == true)
                {
                    Btn_TestMode.Text = "Stop";
                }
                else
                {
                    Btn_TestMode.Text = "停止";
                }

                UIUpDateThread_Start();//V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
            }
            else
            {
                groupBox_Color.Visible = false;
                App_UI_KeyPad_Display_IndexNum();
                Btn_TP1.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP2.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP3.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP4.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP5.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP6.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP7.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP8.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP9.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP10.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP11.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP12.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP13.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP14.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP15.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP16.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP17.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP18.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP19.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP20.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP21.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP22.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP23.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP24.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_TP25.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP26.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP27.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP28.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP29.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP30.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP31.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_TP32.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                Btn_IR1.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR2.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR3.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR4.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR5.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR6.Font = new System.Drawing.Font("SimSun", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR7.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                Btn_IR8.Font = new System.Drawing.Font("SimSun", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));

                bDebugModesFG = false;
                if (bLanguageEN == true)
                {
                    Btn_TestMode.Text = "DebugMode";
                }
                else
                {
                    Btn_TestMode.Text = "调试模式";
                }

                UIUpDateThread_Stop();//V1.0.0.29:20250426(1)Zer Raw Data Display in HID Mode and RGB Ctrl
            }
        }

        /// =============================================================================================
        /// ====    ====
        /// =============================================================================================
        //V1.0.0.26:20250412(3)Zer Raw Data Display in HID Mode
        private void Btn_Col_R_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Red;    ValidColor = 0x00FF0000;
        }
        private void Btn_Col_G_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Green;  ValidColor = 0x0000FF00;
        }
        private void Btn_Col_B_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Blue;   ValidColor = 0x000000FF;
        }
        private void Btn_Col_Y_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Yellow; ValidColor = 0x00FFFF00;
        }
        private void Btn_Col_P_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Fuchsia; ValidColor = 0x00FF00FF;
        }
        private void Btn_Col_C_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.Cyan; ValidColor = 0x0000FFFF;
        }
        private void Btn_Col_W_Click(object sender, EventArgs e)
        {
            label_ValidRGB.BackColor = System.Drawing.Color.White; ValidColor = 0x00FFFFFF;
        }

        private void Btn_Col_User_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = colorDialog1.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                //pictureBox1.BackColor = colorDialog1.Color;
                label_ValidRGB.BackColor = colorDialog1.Color;

                ValidColor = (UInt32)((colorDialog1.Color.R << 16) | (colorDialog1.Color.G << 8) | (colorDialog1.Color.B << 0));
                //label_ValidRGB.Text = ValidColor.ToString("X08");
                //button2.Text = colorDialog1.Color.R.ToString() + "-" + colorDialog1.Color.G.ToString() + "-" + colorDialog1.Color.B.ToString();
            }
        }


        private void Btn_Col_R2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Red; InValidColor = 0x00FF0000;
        }
        private void Btn_Col_G2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Green; InValidColor = 0x0000FF00;
        }
        private void Btn_Col_B2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Blue; InValidColor = 0x000000FF;
        }
        private void Btn_Col_Y2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Yellow; InValidColor = 0x00FFFF00;
        }
        private void Btn_Col_P2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Fuchsia; InValidColor = 0x00FF00FF;
        }
        private void Btn_Col_C2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.Cyan; InValidColor = 0x0000FFFF;
        }
        private void Btn_Col_W2_Click(object sender, EventArgs e)
        {
            label_InValidRGB.BackColor = System.Drawing.Color.White; InValidColor = 0x00FFFFFF;
        }

        private void Btn_Col_User2_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = colorDialog1.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                //pictureBox1.BackColor = colorDialog1.Color;
                label_InValidRGB.BackColor = colorDialog1.Color;
                InValidColor = (UInt32)((colorDialog1.Color.R << 16) | (colorDialog1.Color.G << 8) | (colorDialog1.Color.B << 0));
                //button2.Text = colorDialog1.Color.R.ToString() + "-" + colorDialog1.Color.G.ToString() + "-" + colorDialog1.Color.B.ToString();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            groupBox_Color.Visible = true;

            /*int ReportSize = 64;
            byte[] SendByteBuf = new byte[64];

            SendByteBuf[1] = (byte)((InValidColor >> 16) & 0xff);
            SendByteBuf[2] = (byte)((InValidColor >> 8) & 0xff);
            SendByteBuf[3] = (byte)((InValidColor >> 0) & 0xff);
            SendByteBuf[4] = (byte)((ValidColor >> 16) & 0xff);
            SendByteBuf[5] = (byte)((ValidColor >> 8) & 0xff);
            SendByteBuf[6] = (byte)((ValidColor >> 0) & 0xff);

            USBCtrl.WriteReport_P1(SendByteBuf, ReportSize);
            */

            int ReportSize = 64;
            int Point = 0;
            byte[] SendByteBuf = new byte[64];

            //if (TxFlag == 0)
            //{
            //    TxFlag = 1;
            //}
            //else
            //{
                TxFlag = 0;
            //}
            Point = 1;
            SendByteBuf[Point++] = (byte)TxFlag;

            for (int i = 0; i < 10; i++)
            {
                SendByteBuf[Point++] = (byte)((InValidColor >> 16) & 0xff);
                SendByteBuf[Point++] = (byte)((InValidColor >> 8) & 0xff);
                SendByteBuf[Point++] = (byte)((InValidColor >> 0) & 0xff);
                SendByteBuf[Point++] = (byte)((ValidColor >> 16) & 0xff);
                SendByteBuf[Point++] = (byte)((ValidColor >> 8) & 0xff);
                SendByteBuf[Point++] = (byte)((ValidColor >> 0) & 0xff);

            }

            USBCtrl.WriteReport_P1(SendByteBuf, ReportSize);

            /*
            DialogResult dialogResult = colorDialog1.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                pictureBox1.BackColor = colorDialog1.Color;
                button2.BackColor = colorDialog1.Color;
                button2.Text = colorDialog1.Color.R.ToString() + "-" + colorDialog1.Color.G.ToString() + "-" + colorDialog1.Color.B.ToString();


                Color color = pictureBox1.BackColor;
                //textBox1.Clear();
                //textBox2.Text = color.A.ToString() + "," + color.R.ToString() + "," + color.G.ToString() + "," + color.B.ToString();
            }*/
        }


        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
        //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

    }
}
