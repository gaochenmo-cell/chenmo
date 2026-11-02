using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ConfigAppNS
{
	static class Program
	{
		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		[DllImport("shcore.dll")]
		private static extern int SetProcessDpiAwareness(int value);

		private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

		/// <summary>
		/// The main entry point for the application.
		/// Directly opens Yuanpad 5Key (App_DDR). ADX / Laverita / Yuangeki removed.
		/// </summary>
		[STAThread]
		static void Main()
		{
			EnableDpiAwareness();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			string AppName = CheckAppConfig();
			if (AppName != "")
			{
				bool pblnGotOwnership = false;
				Mutex pobjMyMutex = null;
				try
				{
					pobjMyMutex = new Mutex(true, AppName, out pblnGotOwnership);
					if (pblnGotOwnership)
					{
						Application.Run(new App_DDR());
					}
					else
					{
						MessageBox.Show(AppName + " is already running !!!", "System Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
				Application.Run(new App_DDR());
			}
		}

		/// <summary>
		/// Prefer Per-Monitor DPI (Win8.1+); fall back to System DPI aware.
		/// Manifest also declares dpiAwareness for Windows 10+.
		/// </summary>
		static void EnableDpiAwareness()
		{
			try
			{
				// PROCESS_PER_MONITOR_DPI_AWARE = 2 (Windows 8.1+)
				if (SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE) == 0)
					return;
			}
			catch
			{
				// shcore.dll missing on older Windows ¡ª fall through
			}
			try
			{
				SetProcessDPIAware();
			}
			catch
			{
			}
		}

		static string CheckAppConfig()
		{
			return "PADUEX5";
		}
	}
}
