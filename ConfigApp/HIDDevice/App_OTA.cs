using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HIDLibraryNS;
using Microsoft.Win32.SafeHandles;

namespace ConfigAppNS
{
    /// <summary>
    /// Firmware upgrade dialog - AT32 USB HID IAP (AN0007).
    /// The Artery bootloader only accepts interrupt IN/OUT (WriteFile/ReadFile);
    /// HidD_SetOutputReport / GetInputReport (USBLib_DeviceCtrl*) time out.
    /// </summary>
    public partial class App_OTA : Form
    {
        private const uint FLASH_APP_ADDR = 0x08004000;
        private const uint FLASH_CONFIG_ADDR = 0x0800F800;
        private const int MAX_APP_SIZE = (int)(FLASH_CONFIG_ADDR - FLASH_APP_ADDR); // 0xB800
        private const int MIN_APP_SIZE = 1024;
        /// <summary>Length of the stock image and of every release image (ISP-safe).</summary>
        private const int STOCK_APP_SIZE = 37072;
        private const int CHUNK_1K = 1024;
        private const int HID_PAYLOAD = 60; // 64 - cmd(2) - len(2)

        private readonly HidLibrary usb = new HidLibrary();
        private readonly byte reportId = 0;
        private readonly byte reportSize = 64;

        private string binPath = "";
        private byte[] binData = null;
        private FirmwareImageInfo binInfo = null;
        private bool upgrading = false;
        private BackgroundWorker worker;
        private string lastRxHex = "";
        private string lastIapError = "";
        private int bytesWritten = 0;

        // Version of the board as the main window last read it (GET_MODE), used to warn about
        // a file that does not match the board or does not move its version forward.
        private readonly bool devValid;
        private readonly int devHVer, devFwMajor, devFwMinor;

        /// <summary>
        /// Set when the worker reported success: the image whose version the main window should
        /// see on the board once it re-enumerates (null when the file carried no version).
        /// </summary>
        internal FirmwareImageInfo FlashedImage { get; private set; }

        public App_OTA() : this(false, 0, 0, 0) { }

        public App_OTA(bool deviceVersionValid, int deviceHVer, int deviceFwMajor, int deviceFwMinor)
        {
            devValid = deviceVersionValid;
            devHVer = deviceHVer;
            devFwMajor = deviceFwMajor;
            devFwMinor = deviceFwMinor;
            InitializeComponent();
            try
            {
                string icoPath = Path.Combine(Application.StartupPath, "AppLogo.ico");
                if (File.Exists(icoPath))
                    this.Icon = new Icon(icoPath);
                else
                {
                    Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (ico != null)
                        this.Icon = ico;
                }
            }
            catch { }

            // Fixed pixel layout — avoid Font autoscaling which garbles this dialog on high DPI
            this.AutoScaleMode = AutoScaleMode.None;
            // Same family / fallback chain as the main window; designer sizes stay, 9pt rows grow to body size.
            Font logFont = textLog.Font;
            UiFont.Refamily(this, UiFont.Status);
            textLog.Font = logFont;   // the hex log stays monospace
            ApplyUiLanguage();
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            panelBanner.Visible = false;
            ApplyIdleLayout();
            SetStatus(L.Get("Ota.SelectPrompt"));
        }

        /// <summary>All captions of this dialog come from Strings*.resx (see Localization.cs).</summary>
        private void ApplyUiLanguage()
        {
            this.Text = L.Get("Ota.Title");
            labelTitle.Text = L.Get("Ota.Title");
            labelTips.Text = L.Get("Ota.Tips");
            labelAddr.Text = L.F("Ota.Addr", FLASH_APP_ADDR.ToString("X8"));
            labelLimit.Text = L.F("Ota.Limit", MAX_APP_SIZE);
            labelWarn.Text = L.Get("Ota.Warn");
            btnBrowse.Text = L.Get("Ota.Browse");
            btnStart.Text = L.Get("Ota.Start");
            btnClose.Text = L.Get("Ota.Close");
            if (binData == null)
                labelFileInfo.Text = L.Get("Ota.NoFile");
        }

        /// <summary>Idle layout: title + tips visible; banner hidden.</summary>
        private void ApplyIdleLayout()
        {
            panelBanner.Visible = false;
            labelTitle.Visible = true;
            labelTips.Visible = true;
            labelTitle.Location = new Point(20, 16);
            labelTips.Location = new Point(20, 56);
            // The three tips wrap to up to 7 lines at the larger body font: give the block
            // room for that and shift the rows below it by the same amount.
            const int tipsExtra = 64;
            labelTips.Size = new Size(520, 88 + tipsExtra);
            int y = 156 + tipsExtra;
            labelAddr.Location = new Point(20, y);
            labelLimit.Location = new Point(270, y);
            labelLimit.Size = new Size(270, labelLimit.Height);
            y = 188 + tipsExtra;
            textFile.Location = new Point(20, y);
            btnBrowse.Location = new Point(430, y - 2);
            labelFileInfo.Location = new Point(20, 224 + tipsExtra);
            progressBar.Location = new Point(20, 258 + tipsExtra);
            labelPercent.Location = new Point(480, 258 + tipsExtra);
            labelStatus.Location = new Point(20, 296 + tipsExtra);
            textLog.Location = new Point(20, 328 + tipsExtra);
            textLog.Size = new Size(520, 140);
            btnStart.Location = new Point(20, 484 + tipsExtra);
            btnClose.Location = new Point(430, 484 + tipsExtra);
            this.ClientSize = new Size(560, 540 + tipsExtra);
            this.BackColor = Color.FromArgb(250, 250, 250);
        }

        /// <summary>Busy layout: Laverita-style red banner on top; tips tucked away.</summary>
        private void ApplyBusyLayout()
        {
            panelBanner.Visible = true;
            panelBanner.Location = new Point(0, 0);
            panelBanner.Size = new Size(this.ClientSize.Width, 56);
            labelTitle.Visible = false;
            labelTips.Visible = false;
            int y = 68;
            labelAddr.Location = new Point(20, y);
            labelLimit.Location = new Point(270, y);
            labelLimit.Size = new Size(270, labelLimit.Height);
            y = 100;
            textFile.Location = new Point(20, y);
            btnBrowse.Location = new Point(430, y - 2);
            labelFileInfo.Location = new Point(20, 136);
            progressBar.Location = new Point(20, 170);
            labelPercent.Location = new Point(480, 170);
            labelStatus.Location = new Point(20, 208);
            textLog.Location = new Point(20, 240);
            textLog.Size = new Size(520, 180);
            btnStart.Location = new Point(20, 440);
            btnClose.Location = new Point(430, 440);
            this.ClientSize = new Size(560, 496);
            this.BackColor = Color.FromArgb(255, 248, 240);
        }

        private void SetStatus(string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatus), msg);
                return;
            }
            labelStatus.Text = msg;
            textLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
        }

        private void SetProgress(int percent, string detail)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, string>(SetProgress), percent, detail);
                return;
            }
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            progressBar.Value = percent;
            labelPercent.Text = percent.ToString() + "%";
            if (!string.IsNullOrEmpty(detail))
                labelStatus.Text = detail;
        }

        private void SetUiBusy(bool busy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetUiBusy), busy);
                return;
            }
            upgrading = busy;
            btnBrowse.Enabled = !busy;
            btnStart.Enabled = !busy && binData != null && binData.Length > 0;
            btnClose.Enabled = !busy;
            this.ControlBox = !busy;
            if (busy)
                ApplyBusyLayout();
            else
                ApplyIdleLayout();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = L.Get("Ota.Filter");
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                byte[] data = File.ReadAllBytes(dlg.FileName);
                FirmwareImageInfo info = FirmwareImageInfo.Parse(data);
                DebugLog.Log("OTA: file \"{0}\" size={1} sha256={2} {3}", dlg.FileName, data.Length, info.Sha256, info.VersionLogText());
                string err = ValidateFirmware(data);
                if (err != null)
                {
                    DebugLog.Log("OTA: file rejected: {0}", err);
                    MessageBox.Show(err, L.Get("Ota.CheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                binPath = dlg.FileName;
                binData = data;
                binInfo = info;
                textFile.Text = binPath;
                labelFileInfo.Text = L.F("Ota.FileInfo", binData.Length, Align1K(binData.Length)) + "  |  " + FileVersionText(info);
                btnStart.Enabled = true;
                SetStatus(L.F("Ota.Loaded", Path.GetFileName(binPath)) + "  " + FileVersionText(info));
                SetStatus("SHA-256 " + info.Sha256);
                if (!info.HasVersion)
                    SetStatus(L.Get("Ota.NoVersionInFile"));
                // Images longer than stock are written in full here, but ISP tools that
                // flash by the stock length would truncate them.
                if (binData.Length > STOCK_APP_SIZE)
                {
                    MessageBox.Show(
                        L.F("Ota.SizeWarn", binData.Length, STOCK_APP_SIZE),
                        L.Get("Ota.SizeWarnTitle"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.F("Ota.ReadFail", ex.Message), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Anti-brick: reject bad / oversized images before touching the device.</summary>
        private string ValidateFirmware(byte[] data)
        {
            if (data == null || data.Length < MIN_APP_SIZE)
                return L.F("Ota.V.TooSmall", MIN_APP_SIZE);
            if (data.Length > MAX_APP_SIZE)
                return L.F("Ota.V.TooLarge", MAX_APP_SIZE);
            // Vector table: SP roughly in SRAM (0x2000xxxx), Reset in flash app region
            uint sp = BitConverter.ToUInt32(data, 0);
            uint reset = BitConverter.ToUInt32(data, 4);
            if ((sp & 0xFFF00000) != 0x20000000)
                return L.Get("Ota.V.BadSp");
            if (reset < FLASH_APP_ADDR || reset >= FLASH_CONFIG_ADDR)
                return L.Get("Ota.V.BadReset");
            // IRQ67 (OTGFS1 / USB) vector must point into APP flash; an image with a
            // damaged 0x14C entry crashes on the first USB interrupt.
            if (data.Length > 0x150)
            {
                uint usbIrq = BitConverter.ToUInt32(data, 0x14C);
                if (usbIrq < FLASH_APP_ADDR || usbIrq >= FLASH_CONFIG_ADDR || (usbIrq & 1) == 0)
                    return L.Get("Ota.V.BadUsbVector");
            }
            return null;
        }

        /// <summary>"V3.2 (PADUEX4)" from the version bytes inside the image, or an "unknown" text.</summary>
        private static string FileVersionText(FirmwareImageInfo info)
        {
            if (info == null || !info.HasVersion) return L.Get("Ota.FileVerUnknown");
            return L.F("Ota.FileVer", info.VersionText(), BoardKindText(info.HVer));
        }

        private string BoardVersionText()
        {
            if (!devValid) return L.Get("Ota.BoardVerUnknown");
            return L.F("Ota.BoardVer", FirmwareImageInfo.VersionText(devFwMajor, devFwMinor), BoardKindText(devHVer));
        }

        private static string BoardKindText(int hver)
        {
            if (hver == DeviceProtocol.HVER_5KEY) return L.Get("Ota.Kind5");
            if (hver == DeviceProtocol.HVER_4KEY) return L.Get("Ota.Kind4");
            return L.F("Ota.KindUnknown", hver);
        }

        /// <summary>
        /// Warn when the file obviously does not belong on this board (5-key image on a 4-key
        /// board and vice versa) or does not move the version forward. Both are legitimate in
        /// recovery scenarios, so the user may still go ahead; the decision is logged.
        /// Returns false when the user backs out.
        /// </summary>
        private bool PreflightVersionCheck()
        {
            if (binInfo == null) return true;
            if (!binInfo.HasVersion)
            {
                DebugLog.Log("OTA: preflight: file carries no recognisable version bytes");
                DialogResult r0 = MessageBox.Show(L.Get("Ota.NoVersionConfirm"), L.Get("Ota.CheckTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                DebugLog.Log("OTA: preflight: user chose {0}", r0);
                return r0 == DialogResult.Yes;
            }
            if (!devValid)
            {
                DebugLog.Log("OTA: preflight: board version unknown, file {0}", binInfo.VersionLogText());
                return true;
            }
            if (binInfo.HVer != devHVer && DeviceProtocol.IsKnownHVer(devHVer))
            {
                DebugLog.Log("OTA: preflight: HVer mismatch file=0x{0:X2} board=0x{1:X2}", binInfo.HVer, devHVer);
                DialogResult r1 = MessageBox.Show(
                    L.F("Ota.HVerMismatch", BoardKindText(binInfo.HVer), BoardKindText(devHVer)),
                    L.Get("Ota.CheckTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                DebugLog.Log("OTA: preflight: user chose {0}", r1);
                if (r1 != DialogResult.Yes) return false;
            }
            int cmp = binInfo.CompareVersion(devFwMajor, devFwMinor);
            if (cmp < 0)
            {
                DebugLog.Log("OTA: preflight: file {0} is older than board {1}",
                    binInfo.VersionText(), FirmwareImageInfo.VersionText(devFwMajor, devFwMinor));
                DialogResult r2 = MessageBox.Show(
                    L.F("Ota.Downgrade", binInfo.VersionText(), FirmwareImageInfo.VersionText(devFwMajor, devFwMinor)),
                    L.Get("Ota.CheckTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                DebugLog.Log("OTA: preflight: user chose {0}", r2);
                if (r2 != DialogResult.Yes) return false;
            }
            else if (cmp == 0)
            {
                DebugLog.Log("OTA: preflight: file version {0} equals board version (re-flash)", binInfo.VersionText());
                DialogResult r3 = MessageBox.Show(
                    L.F("Ota.SameVersion", binInfo.VersionText()),
                    L.Get("Ota.CheckTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                DebugLog.Log("OTA: preflight: user chose {0}", r3);
                if (r3 != DialogResult.Yes) return false;
            }
            return true;
        }

        private static int Align1K(int len)
        {
            return ((len + CHUNK_1K - 1) / CHUNK_1K) * CHUNK_1K;
        }

        private byte[] PadTo1K(byte[] data)
        {
            int n = Align1K(data.Length);
            byte[] outb = new byte[n];
            for (int i = 0; i < n; i++) outb[i] = 0xFF;
            Buffer.BlockCopy(data, 0, outb, 0, data.Length);
            return outb;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (binData == null)
            {
                MessageBox.Show(L.Get("Ota.SelectFirst"), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string err = ValidateFirmware(binData);
            if (err != null)
            {
                MessageBox.Show(err, L.Get("Ota.CheckTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!PreflightVersionCheck())
                return;

            DialogResult r = MessageBox.Show(
                L.F("Ota.Confirm", Path.GetFileName(binPath), binData.Length, FLASH_APP_ADDR.ToString("X8"),
                    FileVersionText(binInfo), BoardVersionText()),
                L.Get("Ota.ConfirmTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (r != DialogResult.Yes)
            {
                DebugLog.Log("OTA: cancelled at confirmation");
                return;
            }

            DebugLog.Log("OTA: START file=\"{0}\" size={1} padded={2} sha256={3} {4}; board={5}",
                binPath, binData.Length, Align1K(binData.Length), binInfo.Sha256, binInfo.VersionLogText(), BoardVersionText());
            FlashedImage = null;
            bytesWritten = 0;
            SetUiBusy(true);
            progressBar.Value = 0;
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_Completed;
            worker.RunWorkerAsync(PadTo1K(binData));
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string detail = e.UserState as string;
            SetProgress(e.ProgressPercentage, detail);
            if (!string.IsNullOrEmpty(detail))
                textLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + detail + Environment.NewLine);
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            SetUiBusy(false);
            try { usb.USBLib_Disconnect(); }
            catch { }
            if (e.Error != null)
            {
                DebugLog.Log("OTA: FAILED after {0} bytes: {1}", bytesWritten, e.Error.Message);
                SetStatus(L.F("Ota.Failed", e.Error.Message));
                MessageBox.Show(
                    L.F("Ota.FailedDetail", e.Error.Message),
                    L.Get("App.Name"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            bool ok = e.Result is bool && (bool)e.Result;
            if (ok)
            {
                DebugLog.Log("OTA: DONE {0} bytes written, FINISH acknowledged, jump issued; expected on the board: {1}",
                    bytesWritten, binInfo != null ? binInfo.VersionLogText() : "(unknown)");
                FlashedImage = binInfo;
                SetProgress(100, L.Get("Ota.Done"));
                // The main window verifies the version once the board re-enumerates
                // (App_DDR.Btn_OTA_Click, after this dialog closes).
                MessageBox.Show(L.Get("Ota.Success"), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>Progress line for the dialog log plus the same text in PADUEX5.log.</summary>
        private static void Step(BackgroundWorker bw, int pct, string text)
        {
            DebugLog.Log("OTA: {0}", text);
            bw.ReportProgress(pct, text);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[] image = (byte[])e.Argument;
            BackgroundWorker bw = (BackgroundWorker)sender;
            e.Result = false;
            OtaHidPort port = null;

            try
            {
                Step(bw, 2, L.Get("Ota.P.Detect"));
                EnsureBootloaderReady(bw);

                // Release HIDLibrary so interrupt port can open exclusively
                try { usb.USBLib_Disconnect(); } catch { }
                Thread.Sleep(200);

                Step(bw, 8, L.Get("Ota.P.OpenPort"));
                port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                Step(bw, 10, L.F("Ota.P.PortReady", port.InputReportLen, port.OutputReportLen));
                Thread.Sleep(150);

                byte[] pkt = new byte[64];

                // START 0x5AA1
                Step(bw, 12, L.Get("Ota.P.StartIap"));
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA1;
                if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA1, true, 2500))
                {
                    bool currentPrefix = port.ForceReportIdPrefix ?? (port.OutputReportLen >= 65);
                    Step(bw, 12, L.Get("Ota.P.NoAckRetry") + " " + FailDetail());
                    try { port.Dispose(); } catch { }
                    port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                    port.ForceReportIdPrefix = !currentPrefix;
                    Thread.Sleep(100);
                    Array.Clear(pkt, 0, pkt.Length);
                    pkt[0] = 0x5A; pkt[1] = 0xA1;
                    if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA1, true, 3000))
                        throw new Exception(L.Get("Ota.E.IapStart") + FailDetail());
                }
                DebugLog.Log("OTA: START(5AA1) acknowledged, RX={0}, reportIdPrefix={1}", lastRxHex,
                    port.ForceReportIdPrefix ?? (port.OutputReportLen >= 65));

                // Optional GETADDR 0x5AA7 (non-fatal if device skips)
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA7;
                if (SendCmdReliable(ref port, pkt, 0x5A, 0xA7, true, 1000))
                    Step(bw, 14, L.Get("Ota.P.AddrOk") + " RX=" + lastRxHex);
                else
                    Step(bw, 14, L.Get("Ota.P.SkipGetAddr"));

                int total = image.Length;
                int totalBlocks = Math.Max(1, total / CHUNK_1K);
                for (int offset = 0; offset < total; offset += CHUNK_1K)
                {
                    int blockIndex = offset / CHUNK_1K;
                    int pct = 15 + (blockIndex * 77) / totalBlocks;
                    uint addr = FLASH_APP_ADDR + (uint)offset;
                    bw.ReportProgress(pct, L.F("Ota.P.Write", addr.ToString("X8"), blockIndex + 1, totalBlocks));

                    Array.Clear(pkt, 0, pkt.Length);
                    pkt[0] = 0x5A; pkt[1] = 0xA2;
                    PutBe32(pkt, 2, addr);
                    if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA2, true, 5000))
                        throw new Exception(L.Get("Ota.E.SetAddr") + FailDetail());

                    int blockSent = 0;
                    while (blockSent < CHUNK_1K)
                    {
                        int n = Math.Min(HID_PAYLOAD, CHUNK_1K - blockSent);
                        Array.Clear(pkt, 0, pkt.Length);
                        pkt[0] = 0x5A; pkt[1] = 0xA3;
                        PutBe16(pkt, 2, n);
                        Buffer.BlockCopy(image, offset + blockSent, pkt, 4, n);
                        blockSent += n;
                        bool lastOfBlock = (blockSent >= CHUNK_1K);
                        // The bootloader programs a block only after all 1024 bytes arrived and
                        // acknowledges once per block. A data packet that could not even be
                        // written to the pipe leaves the block short, so it is fatal as well
                        // (it used to be skipped silently and surfaced later as a vague timeout).
                        if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA3, lastOfBlock, lastOfBlock ? 8000 : 0))
                        {
                            throw new Exception((lastOfBlock ? L.Get("Ota.E.BlockAck") : L.F("Ota.E.DataWrite", blockIndex + 1, blockSent - n))
                                + FailDetail());
                        }
                        if (!lastOfBlock)
                            Thread.Sleep(1);
                    }
                    bytesWritten = offset + CHUNK_1K;
                    DebugLog.Log("OTA: block {0}/{1} @0x{2:X8} 1024 bytes acknowledged (RX={3})", blockIndex + 1, totalBlocks, addr, lastRxHex);
                }

                Step(bw, 94, L.Get("Ota.P.Finish"));
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA4;
                if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA4, true, 3000))
                    throw new Exception(L.Get("Ota.E.Finish") + FailDetail());
                DebugLog.Log("OTA: FINISH(5AA4) acknowledged after {0} bytes, RX={1}", bytesWritten, lastRxHex);

                Step(bw, 98, L.Get("Ota.P.Jump"));
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA6;
                PutBe32(pkt, 2, FLASH_APP_ADDR);
                bool jumpAck = SendCmdReliable(ref port, pkt, 0x5A, 0xA6, true, 2000);
                // The bootloader usually resets before it can answer the jump command; the
                // version check after re-enumeration is what tells whether the board took it.
                DebugLog.Log("OTA: JUMP(5AA6) to 0x{0:X8} {1}", FLASH_APP_ADDR, jumpAck ? "acknowledged RX=" + lastRxHex : "not acknowledged" + FailDetail());

                Thread.Sleep(800);
                Step(bw, 100, L.Get("Ota.P.Success"));
                e.Result = true;
            }
            finally
            {
                try { if (port != null) port.Dispose(); } catch { }
            }
        }

        private void EnsureBootloaderReady(BackgroundWorker bw)
        {
            if (ProbeOtaEnumerated())
            {
                Step(bw, 5, L.Get("Ota.P.OtaFound"));
                return;
            }

            Step(bw, 3, L.Get("Ota.P.TryEnterOta"));
            bool sent = false;
            int[] vids = new int[] {
                (int)DeviceType.DDR_KBS_VID,
                (int)DeviceType.DDR_HID_VID
            };
            int[] pids = new int[] {
                (int)DeviceType.DDR_KBS_PID1, (int)DeviceType.DDR_KBS_PID2,
                (int)DeviceType.DDR_HID_PID1, (int)DeviceType.DDR_HID_PID2, (int)DeviceType.DDR_HID_PID3
            };

            foreach (int vid in vids)
            {
                foreach (int pid in pids)
                {
                    try
                    {
                        if (usb.USBLib_Connect(vid, pid, (int)DeviceType.None) == (int)DeviceType.None)
                            continue;
                        byte[] pkt = new byte[64];
                        pkt[0] = (byte)UserCommand.SET_OTA;
                        pkt[1] = (byte)'O';
                        pkt[2] = (byte)'T';
                        pkt[3] = (byte)'A';
                        // APP config channel still uses control transfer — OK here
                        usb.USBLib_DeviceCtrlOutput(0, reportId, reportSize, pkt);
                        sent = true;
                        DebugLog.Log("OTA: SET_OTA sent to VID {0:X4} PID {1:X4}: {2}", vid, pid, DebugLog.Hex(pkt, 0, 4));
                        usb.USBLib_Disconnect();
                        break;
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Log("OTA: SET_OTA to VID {0:X4} PID {1:X4} failed: {2}", vid, pid, ex.Message);
                    }
                }
                if (sent) break;
            }

            if (!sent)
                Step(bw, 4, L.Get("Ota.P.NoApp"));

            for (int i = 0; i < 40; i++)
            {
                Thread.Sleep(250);
                if (ProbeOtaEnumerated())
                {
                    Step(bw, 6, L.Get("Ota.P.OtaConnected") + " (" + ((i + 1) * 250) + " ms)");
                    return;
                }
                bw.ReportProgress(4 + (i % 3), L.F("Ota.P.Waiting", i + 1, 40));
            }
            DebugLog.Log("OTA: bootloader {0:X4}:{1:X4} did not enumerate within 10 s (SET_OTA sent={2})",
                (int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, sent);
            throw new Exception(L.Get("Ota.E.Timeout"));
        }

        private bool ProbeOtaEnumerated()
        {
            try { usb.USBLib_Disconnect(); } catch { }
            try
            {
                int t = usb.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None);
                if (t == (int)DeviceType.None) return false;
                // Disconnect immediately — exclusive interrupt port needs the handle free
                try { usb.USBLib_Disconnect(); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void PutBe16(byte[] buf, int index, int value)
        {
            buf[index] = (byte)((value >> 8) & 0xFF);
            buf[index + 1] = (byte)(value & 0xFF);
        }

        private static void PutBe32(byte[] buf, int index, uint value)
        {
            buf[index] = (byte)((value >> 24) & 0xFF);
            buf[index + 1] = (byte)((value >> 16) & 0xFF);
            buf[index + 2] = (byte)((value >> 8) & 0xFF);
            buf[index + 3] = (byte)(value & 0xFF);
        }

        private string FailDetail()
        {
            string detail = "";
            if (!string.IsNullOrEmpty(lastIapError))
                detail += " (" + lastIapError + ")";
            else
                detail += L.Get("Ota.NoAck");
            if (!string.IsNullOrEmpty(lastRxHex))
                detail += " RX=" + lastRxHex;
            return detail;
        }

        private static string BytesToHex(byte[] buf, int count)
        {
            if (buf == null || count <= 0) return "(empty)";
            int n = Math.Min(count, buf.Length);
            StringBuilder sb = new StringBuilder(n * 3);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(buf[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static int MatchAck(byte[] report, int bytesRead, byte cmdHi, byte cmdLo)
        {
            if (report == null || bytesRead < 4) return 0;
            int limit = Math.Min(bytesRead, report.Length);
            for (int off = 0; off <= 1; off++)
            {
                if (off + 3 >= limit) continue;
                if (report[off] != cmdHi || report[off + 1] != cmdLo) continue;
                if (report[off + 2] == 0xFF && report[off + 3] == 0x00) return 1;
                if (report[off + 2] == 0x00 && report[off + 3] == 0xFF) return -1;
            }
            return 0;
        }

        private bool SendCmdReliable(ref OtaHidPort port, byte[] packet, byte cmdHi, byte cmdLo, bool waitAck, int ackTimeoutMs)
        {
            lastIapError = "";
            lastRxHex = "";
            try
            {
                port.WritePacket(packet);
            }
            catch (Exception ex)
            {
                try
                {
                    port.Dispose();
                    port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                    port.WritePacket(packet);
                }
                catch (Exception ex2)
                {
                    lastIapError = ex.Message + " / " + ex2.Message;
                    return false;
                }
            }

            if (!waitAck) return true;

            bool resent = false;
            int waited = 0;
            while (waited < ackTimeoutMs)
            {
                int slice = Math.Min(400, ackTimeoutMs - waited);
                byte[] report;
                int n;
                bool got;
                try
                {
                    got = port.TryReadReport(slice, out report, out n);
                }
                catch
                {
                    got = false;
                    report = null;
                    n = 0;
                }

                if (got)
                {
                    lastRxHex = BytesToHex(report, Math.Min(n, 8));
                    int match = MatchAck(report, n, cmdHi, cmdLo);
                    if (match > 0) return true;
                    if (match < 0)
                    {
                        lastIapError = "NACK";
                        return false;
                    }
                    waited += slice;
                    continue;
                }

                waited += slice;
                try
                {
                    port.Dispose();
                    port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                }
                catch (Exception ex)
                {
                    lastIapError = ex.Message;
                    return false;
                }

                if (!resent && waited < ackTimeoutMs)
                {
                    resent = true;
                    try { port.WritePacket(packet); }
                    catch (Exception ex)
                    {
                        lastIapError = ex.Message;
                        return false;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Sync HID interrupt port for AT32 IAP (exclusive of HIDLibrary async reader).
        /// </summary>
        private sealed class OtaHidPort : IDisposable
        {
            private const int DIGCF_PRESENT = 0x00000002;
            private const int DIGCF_DEVICEINTERFACE = 0x00000010;
            private const uint GENERIC_READ = 0x80000000;
            private const uint GENERIC_WRITE = 0x40000000;
            private const uint FILE_SHARE_READ = 0x00000001;
            private const uint FILE_SHARE_WRITE = 0x00000002;
            private const uint OPEN_EXISTING = 3;
            private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

            [StructLayout(LayoutKind.Sequential)]
            private struct SP_DEVICE_INTERFACE_DATA
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public int Flags;
                public IntPtr RESERVED;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            private struct SP_DEVICE_INTERFACE_DETAIL_DATA
            {
                public int cbSize;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string DevicePath;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct HIDD_ATTRIBUTES
            {
                public int Size;
                public ushort VendorID;
                public ushort ProductID;
                public ushort VersionNumber;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct HIDP_CAPS
            {
                public ushort Usage;
                public ushort UsagePage;
                public ushort InputReportByteLength;
                public ushort OutputReportByteLength;
                public ushort FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
                public ushort[] Reserved;
                public ushort NumberLinkCollectionNodes;
                public ushort NumberInputButtonCaps;
                public ushort NumberInputValueCaps;
                public ushort NumberInputDataIndices;
                public ushort NumberOutputButtonCaps;
                public ushort NumberOutputValueCaps;
                public ushort NumberOutputDataIndices;
                public ushort NumberFeatureButtonCaps;
                public ushort NumberFeatureValueCaps;
                public ushort NumberFeatureDataIndices;
            }

            [DllImport("hid.dll", SetLastError = true)]
            private static extern void HidD_GetHidGuid(out Guid hidGuid);

            [DllImport("hid.dll", SetLastError = true)]
            private static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

            [DllImport("hid.dll", SetLastError = true)]
            private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

            [DllImport("hid.dll", SetLastError = true)]
            private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

            [DllImport("hid.dll", SetLastError = true)]
            private static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, int flags);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, IntPtr deviceInfoData);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, IntPtr deviceInfoData);

            [DllImport("setupapi.dll", SetLastError = true)]
            private static extern int SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            private SafeFileHandle _handle;
            private FileStream _stream;
            public int InputReportLen { get; private set; }
            public int OutputReportLen { get; private set; }
            public bool? ForceReportIdPrefix;

            public static OtaHidPort Open(ushort vid, ushort pid)
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);
                IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
                    throw new IOException("SetupDiGetClassDevs failed");

                try
                {
                    SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                    ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                    for (int index = 0; SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, index, ref ifData); index++)
                    {
                        int required = 0;
                        SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, ref required, IntPtr.Zero);

                        SP_DEVICE_INTERFACE_DETAIL_DATA detail = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                        detail.cbSize = IntPtr.Size == 8 ? 8 : 5;

                        if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, ref detail, required, ref required, IntPtr.Zero))
                            continue;

                        SafeFileHandle handle = CreateFile(
                            detail.DevicePath,
                            GENERIC_READ | GENERIC_WRITE,
                            FILE_SHARE_READ | FILE_SHARE_WRITE,
                            IntPtr.Zero,
                            OPEN_EXISTING,
                            FILE_FLAG_OVERLAPPED,
                            IntPtr.Zero);

                        if (handle == null || handle.IsInvalid)
                            continue;

                        try
                        {
                            HIDD_ATTRIBUTES attrs = new HIDD_ATTRIBUTES();
                            attrs.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                            if (!HidD_GetAttributes(handle.DangerousGetHandle(), ref attrs))
                            {
                                handle.Close();
                                continue;
                            }

                            if (attrs.VendorID != vid || attrs.ProductID != pid)
                            {
                                handle.Close();
                                continue;
                            }

                            IntPtr preparsed;
                            if (!HidD_GetPreparsedData(handle, out preparsed))
                            {
                                handle.Close();
                                continue;
                            }

                            HIDP_CAPS caps = new HIDP_CAPS();
                            caps.Reserved = new ushort[17];
                            HidP_GetCaps(preparsed, ref caps);
                            HidD_FreePreparsedData(preparsed);

                            int inLen = caps.InputReportByteLength > 0 ? caps.InputReportByteLength : 65;
                            int outLen = caps.OutputReportByteLength > 0 ? caps.OutputReportByteLength : 65;
                            int bufSize = Math.Max(Math.Max(inLen, outLen), 65);

                            FileStream stream = new FileStream(handle, FileAccess.ReadWrite, bufSize, true);
                            OtaHidPort port = new OtaHidPort();
                            port._handle = handle;
                            port._stream = stream;
                            port.InputReportLen = inLen;
                            port.OutputReportLen = outLen;
                            return port;
                        }
                        catch
                        {
                            try { handle.Close(); } catch { }
                        }
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(devInfo);
                }

                throw new IOException(L.Get("Ota.E.PortNotFound"));
            }

            public void WritePacket(byte[] packet64)
            {
                if (packet64 == null || packet64.Length < 2)
                    throw new ArgumentException("packet");

                bool usePrefix = ForceReportIdPrefix.HasValue
                    ? ForceReportIdPrefix.Value
                    : (OutputReportLen >= 65);

                if (usePrefix)
                {
                    int len = Math.Max(OutputReportLen, 65);
                    byte[] outBuf = new byte[len];
                    outBuf[0] = 0x00;
                    Buffer.BlockCopy(packet64, 0, outBuf, 1, Math.Min(64, len - 1));
                    _stream.Write(outBuf, 0, outBuf.Length);
                }
                else
                {
                    byte[] outBuf = new byte[OutputReportLen];
                    Buffer.BlockCopy(packet64, 0, outBuf, 0, Math.Min(packet64.Length, OutputReportLen));
                    _stream.Write(outBuf, 0, outBuf.Length);
                }
                _stream.Flush();
            }

            public bool TryReadReport(int timeoutMs, out byte[] report, out int bytesRead)
            {
                report = new byte[InputReportLen];
                bytesRead = 0;
                IAsyncResult ar = _stream.BeginRead(report, 0, report.Length, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    return false;
                bytesRead = _stream.EndRead(ar);
                return bytesRead > 0;
            }

            public void Dispose()
            {
                try { if (_stream != null) _stream.Dispose(); } catch { }
                _stream = null;
                try { if (_handle != null && !_handle.IsClosed) _handle.Close(); } catch { }
                _handle = null;
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (upgrading)
            {
                MessageBox.Show(L.Get("Ota.BusyClose"), L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (upgrading)
            {
                e.Cancel = true;
                MessageBox.Show(L.Get("Ota.BusyCloseForm"),
                    L.Get("App.Name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { usb.USBLib_Disconnect(); }
            catch { }
            base.OnFormClosing(e);
        }
    }
}
