using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using HIDLibraryNS;

namespace ConfigAppNS
{
    /// <summary>
    /// PADUEX5 firmware upgrade — AT32 USB HID IAP (AN0007) with progress + anti-brick guards.
    /// </summary>
    public partial class App_OTA : Form
    {
        // AT32 HID IAP commands (MSB, LSB) — AN0007
        private const ushort IAP_CMD_ENTER = 0x5AA0;
        private const ushort IAP_CMD_START = 0x5AA1;
        private const ushort IAP_CMD_ADDR = 0x5AA2;
        private const ushort IAP_CMD_DATA = 0x5AA3;
        private const ushort IAP_CMD_FINISH = 0x5AA4;
        private const ushort IAP_CMD_CRC = 0x5AA5;
        private const ushort IAP_CMD_JMP = 0x5AA6;
        private const ushort IAP_CMD_GETADDR = 0x5AA7;
        private const ushort IAP_ACK = 0xFF00;
        private const ushort IAP_NACK = 0x00FF;

        // Match Yuanpad / uandancedance layout
        private const uint FLASH_APP_ADDR = 0x08004000;
        private const uint FLASH_CONFIG_ADDR = 0x0800F800; // do not overwrite app config pages
        private const int MAX_APP_SIZE = (int)(FLASH_CONFIG_ADDR - FLASH_APP_ADDR); // 0xB800
        private const int MIN_APP_SIZE = 1024;
        private const int CHUNK_1K = 1024;
        private const int HID_PAYLOAD = 60; // 64 - cmd(2) - len(2)

        private readonly HidLibrary usb = new HidLibrary();
        private readonly byte reportId = 0;
        private readonly byte reportSize = 64;
        private byte ioOffset = 0; // OTA bootloader usually has no Report ID

        private string binPath = "";
        private byte[] binData = null;
        private bool upgrading = false;
        private BackgroundWorker worker;

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
            this.Text = "PADUEX5 Firmware Upgrade";
            labelTitle.Text = "PADUEX5 固件升级";
            labelAddr.Text = "APP 地址: 0x" + FLASH_APP_ADDR.ToString("X8");
            labelLimit.Text = "最大允许: " + MAX_APP_SIZE + " 字节（保护配置区）";
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            SetStatus("请选择固件 .bin，然后开始升级。");
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
                "防砖措施：校验向量表 / 限制不覆盖配置区 / 写完并 CRC 通过后才跳转；\r\n" +
                "若中途失败，设备会留在 Bootloader，可再次升级，不会锁死。\r\n\r\n" +
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
                    "升级失败: " + e.Error.Message + "\r\n\r\n设备应仍在 Bootloader（OTA）模式，可重新连接后再次升级，一般不会变砖。",
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

            // 1) Enter bootloader from APP if needed
            bw.ReportProgress(2, "检测设备…");
            EnsureOtaDevice(bw);

            // 2) Verify IAP app address
            bw.ReportProgress(8, "读取 Bootloader APP 地址…");
            uint iapAddr = IapGetAppAddress();
            if (iapAddr != FLASH_APP_ADDR)
                throw new Exception("Bootloader APP 地址为 0x" + iapAddr.ToString("X8") +
                    "，与预期 0x" + FLASH_APP_ADDR.ToString("X8") + " 不一致，已中止以防写错分区。");

            // 3) Start download
            bw.ReportProgress(12, "开始下载…");
            IapExpectAck(IAP_CMD_START, IapSendCmd(IAP_CMD_START), "START");

            int total = image.Length;
            int done = 0;
            uint addr = FLASH_APP_ADDR;

            while (done < total)
            {
                // Set address every 1KB
                IapExpectAck(IAP_CMD_ADDR, IapSendAddr(IAP_CMD_ADDR, addr), "ADDR 0x" + addr.ToString("X8"));

                int block = Math.Min(CHUNK_1K, total - done);
                int sentInBlock = 0;
                while (sentInBlock < block)
                {
                    int n = Math.Min(HID_PAYLOAD, block - sentInBlock);
                    byte[] slice = new byte[n];
                    Buffer.BlockCopy(image, done + sentInBlock, slice, 0, n);
                    // Data packets: only ACK after full 1KB accumulated on device
                    IapSendData(slice);
                    sentInBlock += n;
                }
                // Wait ACK for this 1KB
                IapExpectAck(IAP_CMD_DATA, IapReadResponse(), "DATA @0x" + addr.ToString("X8"));

                done += block;
                addr += (uint)block;
                int pct = 12 + (int)(78.0 * done / total);
                bw.ReportProgress(pct, "写入 " + done + " / " + total + " 字节");
            }

            // 4) Finish
            bw.ReportProgress(92, "发送下载结束…");
            IapExpectAck(IAP_CMD_FINISH, IapSendCmd(IAP_CMD_FINISH), "FINISH");

            // 5) CRC verify — anti-brick: no jump if mismatch
            bw.ReportProgress(95, "CRC 校验…");
            uint localCrc = CalcCrcAt32(image);
            uint remoteCrc = IapCrc(FLASH_APP_ADDR, (ushort)(image.Length / CHUNK_1K));
            if (localCrc != remoteCrc)
                throw new Exception("CRC 不一致（本地 0x" + localCrc.ToString("X8") +
                    " / 设备 0x" + remoteCrc.ToString("X8") + "）。未跳转，可重新升级。");

            // 6) Jump only after verified write
            bw.ReportProgress(98, "跳转 APP…");
            IapExpectAck(IAP_CMD_JMP, IapSendAddr(IAP_CMD_JMP, FLASH_APP_ADDR), "JMP");

            Thread.Sleep(800);
            bw.ReportProgress(100, "升级成功");
            e.Result = true;
        }

        private void EnsureOtaDevice(BackgroundWorker bw)
        {
            // Already in OTA?
            if (TryConnectOta())
            {
                bw.ReportProgress(5, "已连接 OTA Bootloader");
                return;
            }

            // Try ask APP to enter OTA (SET_OTA)
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
                bw.ReportProgress(4, "未找到运行中的 APP，等待手动进入 / 已在 Bootloader…");

            // Wait for OTA device
            for (int i = 0; i < 40; i++)
            {
                Thread.Sleep(250);
                if (TryConnectOta())
                {
                    bw.ReportProgress(6, "OTA Bootloader 已连接");
                    return;
                }
                bw.ReportProgress(4 + (i % 3), "等待 OTA 设备… (" + (i + 1) + "/40)");
            }
            throw new Exception("超时未检测到 OTA 设备 (VID 0x0E8F / PID 0x8901)。请确认设备已进入升级模式。");
        }

        private bool TryConnectOta()
        {
            try
            {
                usb.USBLib_Disconnect();
            }
            catch { }
            try
            {
                int t = usb.USBLib_Connect((int)DeviceType.HID_OTA_VID, (int)DeviceType.HID_OTA_PID, (int)DeviceType.None);
                return t != (int)DeviceType.None;
            }
            catch
            {
                return false;
            }
        }

        private byte[] IapSendCmd(ushort cmd)
        {
            byte[] pkt = new byte[64];
            pkt[0] = (byte)((cmd >> 8) & 0xFF);
            pkt[1] = (byte)(cmd & 0xFF);
            usb.USBLib_DeviceCtrlOutput(0, reportId, reportSize, pkt);
            Thread.Sleep(20);
            return IapReadResponse();
        }

        private byte[] IapSendAddr(ushort cmd, uint addr)
        {
            byte[] pkt = new byte[64];
            pkt[0] = (byte)((cmd >> 8) & 0xFF);
            pkt[1] = (byte)(cmd & 0xFF);
            pkt[2] = (byte)((addr >> 24) & 0xFF);
            pkt[3] = (byte)((addr >> 16) & 0xFF);
            pkt[4] = (byte)((addr >> 8) & 0xFF);
            pkt[5] = (byte)(addr & 0xFF);
            usb.USBLib_DeviceCtrlOutput(0, reportId, reportSize, pkt);
            Thread.Sleep(30);
            return IapReadResponse();
        }

        private void IapSendData(byte[] data)
        {
            byte[] pkt = new byte[64];
            pkt[0] = (byte)((IAP_CMD_DATA >> 8) & 0xFF);
            pkt[1] = (byte)(IAP_CMD_DATA & 0xFF);
            pkt[2] = (byte)((data.Length >> 8) & 0xFF);
            pkt[3] = (byte)(data.Length & 0xFF);
            Buffer.BlockCopy(data, 0, pkt, 4, data.Length);
            usb.USBLib_DeviceCtrlOutput(0, reportId, reportSize, pkt);
            Thread.Sleep(2);
        }

        private byte[] IapReadResponse()
        {
            byte[] rsp = new byte[64];
            // Try offset 0 then 1 (some stacks insert Report ID)
            bool ok = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Array.Clear(rsp, 0, rsp.Length);
                try
                {
                    usb.USBLib_DeviceCtrlInput(0, ioOffset, reportId, reportSize, rsp);
                    if (rsp[0] == 0x5A)
                    {
                        ok = true;
                        break;
                    }
                    // Maybe report ID present
                    if (rsp[1] == 0x5A)
                    {
                        ioOffset = 1;
                        byte[] shifted = new byte[64];
                        Buffer.BlockCopy(rsp, 1, shifted, 0, 63);
                        return shifted;
                    }
                }
                catch { }
                Thread.Sleep(20);
            }
            if (!ok)
                throw new Exception("读取 IAP 应答超时。");
            return rsp;
        }

        private void IapExpectAck(ushort cmd, byte[] rsp, string step)
        {
            if (rsp == null || rsp.Length < 4)
                throw new Exception(step + ": 空应答");
            ushort rcmd = (ushort)((rsp[0] << 8) | rsp[1]);
            ushort ack = (ushort)((rsp[2] << 8) | rsp[3]);
            if (rcmd != cmd)
                throw new Exception(step + ": 命令不匹配 (0x" + rcmd.ToString("X4") + ")");
            if (ack != IAP_ACK)
                throw new Exception(step + ": 设备返回 NACK，已中止（未跳转）。");
        }

        private uint IapGetAppAddress()
        {
            byte[] rsp = IapSendCmd(IAP_CMD_GETADDR);
            IapExpectAck(IAP_CMD_GETADDR, rsp, "GETADDR");
            if (rsp.Length < 8)
                throw new Exception("GETADDR 应答过短");
            return ((uint)rsp[4] << 24) | ((uint)rsp[5] << 16) | ((uint)rsp[6] << 8) | rsp[7];
        }

        private uint IapCrc(uint addr, ushort sizeKb)
        {
            byte[] pkt = new byte[64];
            pkt[0] = (byte)((IAP_CMD_CRC >> 8) & 0xFF);
            pkt[1] = (byte)(IAP_CMD_CRC & 0xFF);
            pkt[2] = (byte)((addr >> 24) & 0xFF);
            pkt[3] = (byte)((addr >> 16) & 0xFF);
            pkt[4] = (byte)((addr >> 8) & 0xFF);
            pkt[5] = (byte)(addr & 0xFF);
            pkt[6] = (byte)((sizeKb >> 8) & 0xFF);
            pkt[7] = (byte)(sizeKb & 0xFF);
            usb.USBLib_DeviceCtrlOutput(0, reportId, reportSize, pkt);
            Thread.Sleep(50);
            byte[] rsp = IapReadResponse();
            IapExpectAck(IAP_CMD_CRC, rsp, "CRC");
            if (rsp.Length < 8)
                throw new Exception("CRC 应答过短");
            return ((uint)rsp[4] << 24) | ((uint)rsp[5] << 16) | ((uint)rsp[6] << 8) | rsp[7];
        }

        /// <summary>AT32 hardware-style CRC32 (poly 0x04C11DB7, init 0xFFFFFFFF, no reflect).</summary>
        private static uint CalcCrcAt32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            // Process as 32-bit little-endian words (pad 0xFF already applied)
            for (int i = 0; i + 4 <= data.Length; i += 4)
            {
                uint w = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                crc ^= w;
                for (int b = 0; b < 32; b++)
                {
                    if ((crc & 0x80000000) != 0)
                        crc = (crc << 1) ^ 0x04C11DB7;
                    else
                        crc <<= 1;
                }
            }
            return crc;
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
