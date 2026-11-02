using System;
using System.Windows.Forms;

namespace ConfigAppNS
{
	/// <summary>
	/// Legacy launcher form — no longer used.
	/// Entry point opens App_DDR (Yuanpad 5Key) directly.
	/// Kept as an empty stub so old designer references do not break Mono builds.
	/// </summary>
	public partial class App_Main : Form
	{
		public App_Main()
		{
			InitializeComponent();
		}
	}
}
