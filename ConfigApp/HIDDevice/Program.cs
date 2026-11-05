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

		/// <summary>
		/// Entry: Yuanpad 5Key only.
		/// Use System DPI awareness (.NET 4.0 WinForms). Per-Monitor V2 is unreliable
		/// on FX 4.0 and left the UI unscaled in a large client area.
		/// </summary>
		[STAThread]
		static void Main()
		{
			try { SetProcessDPIAware(); } catch { }

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			// Language: Config.ini LANGUAGE=AUTO|CN|EN (missing/AUTO = follow the Windows UI language)
			L.Apply(AppConfig.Read("LANGUAGE"));

			string AppName = "PADUEX5";
			bool gotOwnership = false;
			Mutex mutex = null;
			try
			{
				mutex = new Mutex(true, AppName, out gotOwnership);
				if (!gotOwnership)
				{
					MessageBox.Show(L.F("App.AlreadyRunning", AppName), L.Get("Msg.SystemTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				Application.Run(new App_DDR());
			}
			finally
			{
				if (mutex != null)
					mutex.Close();
			}
		}
	}
}
