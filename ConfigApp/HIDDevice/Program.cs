using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using HIDLibraryNS;
using System.Threading;
using System.Runtime.InteropServices;



namespace ConfigAppNS
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //------------------------------------------------------------------------------------------------------------------
            /*判断程序是否运行*/
            string AppName = "";
            AppName = CheckAppConfig();
            if (AppName != "")
            {
                bool pblnGotOwnership = false;
                Mutex pobjMyMutex = null;
                try
                {
                    pobjMyMutex = new Mutex(true, AppName, out   pblnGotOwnership);
                    if (pblnGotOwnership)
                    {
                        Application.Run(new App_Main());
                    }
                    else
                    {
                        MessageBox.Show( AppName +" is already running !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    if (pobjMyMutex != null)
                        pobjMyMutex.Close();
                }
            }
            else
            {
                Application.Run(new App_Main());
            }
            //------------------------------------------------------------------------------------------------------------------
		}

        /// ==================================
        /// ==== ReadAppConfig ====
        /// ==================================
        static string CheckAppConfig()
        {
            //------------------------------------------------------------------------------------------------------------------
            //string Currentpath = Directory.GetCurrentDirectory() + "\\Config.ini";
            string TempAppName = "Config App";
            /*string Str = "";
            //------------------------------------------------------------------------------------------------------------------
            using (StreamReader Rhandle = File.OpenText(Currentpath))         // Create a file to write to.
            {
                for (byte i = 0; i < 5; i++)
                {
                    Str = Rhandle.ReadLine();
                    if (Str == "[RFID Config]")
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
                                Str = StrTab[1];
                                S = Str.Split(' ');
                                TempAppName = "SpyroProtalApp";
                            }
                            else if (Str == "CHECKAPPRUNNING")
                            {
                                if (StrTab[1] == "NO")
                                {
                                    TempAppName="";
                                }
                                break;
                            }
                        }
                        break;
                    }
                }
            }*/
            return TempAppName;
            //------------------------------------------------------------------------------------------------------------------
        }




	}
}