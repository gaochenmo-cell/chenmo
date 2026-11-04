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
    /// PADUEX5 firmware upgrade — AT32 USB HID IAP (AN0007).
    /// Artery bootloader only accepts interrupt IN/OUT (WriteFile/ReadFile).
    /// HidD_SetOutputReport / GetInputReport (USBLib_DeviceCtrl*) will time out — same fix as LaveritaConfig.
    /// </summary>
    public partial class App_OTA : Form
    {
        private const uint FLASH_APP_ADDR = 0x08004000;
        private const uint FLASH_CONFIG_ADDR = 0x0800F800;
        private const int MAX_APP_SIZE = (int)(FLASH_CONFIG_ADDR - FLASH_APP_ADDR); // 0xB800
        private const int MIN_APP_SIZE = 1024;
        /// <summary>Stock / grok1.0 / grok1.2 / grok1.4 image length — ISP-safe.</summary>
        private const int STOCK_APP_SIZE = 37072;
        private const int CHUNK_1K = 1024;
        private const int HID_PAYLOAD = 60; // 64 - cmd(2) - len(2)

        private readonly HidLibrary usb = new HidLibrary();
        private readonly byte reportId = 0;
        private readonly byte reportSize = 64;

        private string binPath = "";
        private byte[] binData = null;
        private bool upgrading = false;
        private BackgroundWorker worker;
        private string lastRxHex = "";
        private string lastIapError = "";

        public App_OTA()
        {
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
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.Text = "PADUEX5 固件升级";
            labelTitle.Text = "PADUEX5 固件升级";
            labelTips.Text =
                "操作提示：\r\n" +
                "1. 选择 firmware\\grok1.4.bin（同体积；改键 + 粘键超时；勿用 grok1.3）\r\n" +
                "2. 保持 USB 连接，升级中勿拔线、勿关机、勿关闭本窗口\r\n" +
                "3. 失败留在 Bootloader 可重试；ISP 请用 grok1.4/1.2/1.0（勿用 grok1.3 / grok1.1）";
            labelAddr.Text = "APP 地址: 0x" + FLASH_APP_ADDR.ToString("X8");
            labelLimit.Text = "最大允许: " + MAX_APP_SIZE + " 字节（保护配置区）";
            labelWarn.Text = "升级中，请勿关机！";
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            panelBanner.Visible = false;
            ApplyIdleLayout();
            SetStatus("请选择固件 .bin，然后开始升级。");
        }

        /// <summary>Idle layout: title + tips visible; banner hidden.</summary>
        private void ApplyIdleLayout()
        {
            panelBanner.Visible = false;
            labelTitle.Visible = true;
            labelTips.Visible = true;
            labelTitle.Location = new Point(20, 16);
            labelTips.Location = new Point(20, 56);
            int y = 156;
            labelAddr.Location = new Point(20, y);
            labelLimit.Location = new Point(280, y);
            y = 188;
            textFile.Location = new Point(20, y);
            btnBrowse.Location = new Point(430, y - 2);
            labelFileInfo.Location = new Point(20, 224);
            progressBar.Location = new Point(20, 258);
            labelPercent.Location = new Point(480, 258);
            labelStatus.Location = new Point(20, 296);
            textLog.Location = new Point(20, 328);
            textLog.Size = new Size(520, 140);
            btnStart.Location = new Point(20, 484);
            btnClose.Location = new Point(430, 484);
            this.ClientSize = new Size(560, 540);
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
            labelLimit.Location = new Point(280, y);
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
            dlg.Filter = "Firmware (*.bin)|*.bin|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                byte[] data = File.ReadAllBytes(dlg.FileName);
                string err = ValidateFirmware(data);
                if (err != null)
                {
                    MessageBox.Show(err, "PADUEX5 固件检查", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                binPath = dlg.FileName;
                binData = data;
                textFile.Text = binPath;
                labelFileInfo.Text = "大小: " + binData.Length + " 字节  |  对齐后: " + Align1K(binData.Length) + " 字节";
                btnStart.Enabled = true;
                SetStatus("已加载固件: " + Path.GetFileName(binPath));
                // Soft warn: images longer than stock (e.g. grok1.1 cave) are OK for this OTA,
                // but ISP tools that flash by old length can truncate and brick.
                if (binData.Length > STOCK_APP_SIZE)
                {
                    MessageBox.Show(
                        "此固件比原厂镜像长（" + binData.Length + " > " + STOCK_APP_SIZE + " 字节）。\r\n\r\n" +
                        "本窗口 OTA 会整包写入，一般可用。\r\n" +
                        "若改用厂家 ISP/IPS：请改刷 grok1.4.bin / grok1.2.bin / grok1.0.bin（同体积；勿用 grok1.3），" +
                        "地址 0x08004000，否则可能截断末尾导致 USB 无法识别。",
                        "PADUEX5 体积提示",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取固件失败: " + ex.Message, "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Anti-brick: reject bad / oversized images before touching the device.</summary>
        private string ValidateFirmware(byte[] data)
        {
            if (data == null || data.Length < MIN_APP_SIZE)
                return "固件太小（<" + MIN_APP_SIZE + "），已拒绝，避免写入无效镜像。";
            if (data.Length > MAX_APP_SIZE)
                return "固件过大（>" + MAX_APP_SIZE + "），会覆盖配置区，已拒绝以防变砖。";
            // Vector table: SP roughly in SRAM (0x2000xxxx), Reset in flash app region
            uint sp = BitConverter.ToUInt32(data, 0);
            uint reset = BitConverter.ToUInt32(data, 4);
            if ((sp & 0xFFF00000) != 0x20000000)
                return "固件向量表栈顶异常（非 SRAM），可能不是本机 APP，已拒绝。";
            if (reset < FLASH_APP_ADDR || reset >= FLASH_CONFIG_ADDR)
                return "固件复位向量不在 APP 区 (0x08004000~0x0800F800)，已拒绝以防变砖。";
            return null;
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
                MessageBox.Show("请先选择固件文件。", "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string err = ValidateFirmware(binData);
            if (err != null)
            {
                MessageBox.Show(err, "PADUEX5 固件检查", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult r = MessageBox.Show(
                "即将升级 PADUEX5 固件。\r\n\r\n" +
                "文件: " + Path.GetFileName(binPath) + "\r\n" +
                "大小: " + binData.Length + " 字节\r\n" +
                "地址: 0x" + FLASH_APP_ADDR.ToString("X8") + "\r\n\r\n" +
                "请注意：\r\n" +
                "· 升级中请勿拔 USB、勿关机、勿关闭本窗口\r\n" +
                "· 写完并 CRC 通过后才会跳转；失败则留在 Bootloader，可重试\r\n" +
                "· 推荐刷入 grok1.4.bin（改键 + 粘键超时，栈对齐已修；勿用 grok1.3）\r\n" +
                "· 勿再用 ISP 刷 grok1.1.bin（末尾加长，易截断变砖）\r\n\r\n" +
                "确认开始？",
                "PADUEX5 升级确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (r != DialogResult.Yes)
                return;

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
                SetStatus("升级失败: " + e.Error.Message);
                MessageBox.Show(
                    "升级失败: " + e.Error.Message + "\r\n\r\n" +
                    "设备应仍在 Bootloader（OTA）模式。\r\n" +
                    "请保持 USB 连接，关闭本提示后再次点「开始升级」。一般不会变砖。",
                    "PADUEX5",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            bool ok = e.Result is bool && (bool)e.Result;
            if (ok)
            {
                SetProgress(100, "升级完成");
                MessageBox.Show("固件升级成功！设备已跳转运行新固件。", "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[] image = (byte[])e.Argument;
            BackgroundWorker bw = (BackgroundWorker)sender;
            e.Result = false;
            OtaHidPort port = null;

            try
            {
                bw.ReportProgress(2, "检测设备…");
                EnsureBootloaderReady(bw);

                // Release HIDLibrary so interrupt port can open exclusively
                try { usb.USBLib_Disconnect(); } catch { }
                Thread.Sleep(200);

                bw.ReportProgress(8, "打开 Bootloader HID 中断口…");
                port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                bw.ReportProgress(10, "IAP 端口就绪 (IN=" + port.InputReportLen + ", OUT=" + port.OutputReportLen + ")");
                Thread.Sleep(150);

                byte[] pkt = new byte[64];

                // START 0x5AA1
                bw.ReportProgress(12, "开始 IAP 下载…");
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA1;
                if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA1, true, 2500))
                {
                    bool currentPrefix = port.ForceReportIdPrefix ?? (port.OutputReportLen >= 65);
                    bw.ReportProgress(12, "无 ACK，切换 HID 帧格式重试…");
                    try { port.Dispose(); } catch { }
                    port = OtaHidPort.Open((ushort)DeviceType.HID_OTA_VID, (ushort)DeviceType.HID_OTA_PID);
                    port.ForceReportIdPrefix = !currentPrefix;
                    Thread.Sleep(100);
                    Array.Clear(pkt, 0, pkt.Length);
                    pkt[0] = 0x5A; pkt[1] = 0xA1;
                    if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA1, true, 3000))
                        throw new Exception("IAP 启动失败" + FailDetail());
                }

                // Optional GETADDR 0x5AA7 (non-fatal if device skips)
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA7;
                if (SendCmdReliable(ref port, pkt, 0x5A, 0xA7, true, 1000))
                    bw.ReportProgress(14, "Bootloader APP 地址确认 OK");
                else
                    bw.ReportProgress(14, "跳过 GETADDR（部分 Bootloader 不回），继续写入…");

                int total = image.Length;
                for (int offset = 0; offset < total; offset += CHUNK_1K)
                {
                    int blockIndex = offset / CHUNK_1K;
                    int totalBlocks = Math.Max(1, total / CHUNK_1K);
                    int pct = 15 + (blockIndex * 77) / totalBlocks;
                    uint addr = FLASH_APP_ADDR + (uint)offset;
                    bw.ReportProgress(pct, "写入 0x" + addr.ToString("X8") + " (" + (blockIndex + 1) + "/" + totalBlocks + ")");

                    Array.Clear(pkt, 0, pkt.Length);
                    pkt[0] = 0x5A; pkt[1] = 0xA2;
                    PutBe32(pkt, 2, addr);
                    if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA2, true, 5000))
                        throw new Exception("设置地址失败" + FailDetail());

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
                        if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA3, lastOfBlock, lastOfBlock ? 8000 : 0))
                        {
                            if (lastOfBlock)
                                throw new Exception("数据块 ACK 失败" + FailDetail());
                        }
                        else if (!lastOfBlock)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }

                bw.ReportProgress(94, "完成下载…");
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA4;
                if (!SendCmdReliable(ref port, pkt, 0x5A, 0xA4, true, 3000))
                    throw new Exception("完成命令失败" + FailDetail());

                bw.ReportProgress(98, "跳转应用…");
                Array.Clear(pkt, 0, pkt.Length);
                pkt[0] = 0x5A; pkt[1] = 0xA6;
                PutBe32(pkt, 2, FLASH_APP_ADDR);
                SendCmdReliable(ref port, pkt, 0x5A, 0xA6, true, 2000);

                Thread.Sleep(800);
                bw.ReportProgress(100, "升级成功");
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
                bw.ReportProgress(5, "已检测到 OTA Bootloader");
                return;
            }

            bw.ReportProgress(3, "尝试让 APP 进入 OTA…");
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
                        usb.USBLib_Disconnect();
                        break;
                    }
                    catch { }
                }
                if (sent) break;
            }

            if (!sent)
                bw.ReportProgress(4, "未找到运行中的 APP，等待 Bootloader…");

            for (int i = 0; i < 40; i++)
            {
                Thread.Sleep(250);
                if (ProbeOtaEnumerated())
                {
                    bw.ReportProgress(6, "OTA Bootloader 已连接");
                    return;
                }
                bw.ReportProgress(4 + (i % 3), "等待 OTA 设备… (" + (i + 1) + "/40)");
            }
            throw new Exception("超时未检测到 OTA 设备 (VID 0x0E8F / PID 0x8901)。请确认设备已进入升级模式。");
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
                detail += "（无 ACK）";
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

                throw new IOException("OTA HID 设备未找到 (0x0E8F:0x8901)");
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
                MessageBox.Show("升级进行中，请勿关闭。", "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (upgrading)
            {
                e.Cancel = true;
                MessageBox.Show("升级进行中，请勿关闭。中断可能导致需重新升级（Bootloader 仍可进入）。",
                    "PADUEX5", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { usb.USBLib_Disconnect(); }
            catch { }
            base.OnFormClosing(e);
        }
    }
}
