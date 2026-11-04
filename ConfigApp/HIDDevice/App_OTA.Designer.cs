namespace ConfigAppNS
{
    partial class App_OTA
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.panelBanner = new System.Windows.Forms.Panel();
            this.labelWarn = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.labelTips = new System.Windows.Forms.Label();
            this.labelAddr = new System.Windows.Forms.Label();
            this.labelLimit = new System.Windows.Forms.Label();
            this.textFile = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelFileInfo = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.labelPercent = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.textLog = new System.Windows.Forms.TextBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.panelBanner.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelBanner — Laverita-style warning strip (shown while upgrading)
            // 
            this.panelBanner.BackColor = System.Drawing.Color.FromArgb(255, 236, 220);
            this.panelBanner.Controls.Add(this.labelWarn);
            this.panelBanner.Location = new System.Drawing.Point(0, 0);
            this.panelBanner.Name = "panelBanner";
            this.panelBanner.Size = new System.Drawing.Size(560, 56);
            this.panelBanner.TabIndex = 0;
            this.panelBanner.Visible = false;
            // 
            // labelWarn
            // 
            this.labelWarn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelWarn.Font = new System.Drawing.Font("Microsoft YaHei UI", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelWarn.ForeColor = System.Drawing.Color.FromArgb(180, 20, 20);
            this.labelWarn.Name = "labelWarn";
            this.labelWarn.Size = new System.Drawing.Size(560, 56);
            this.labelWarn.TabIndex = 0;
            this.labelWarn.Text = "升级中，请勿关机！";
            this.labelWarn.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = false;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTitle.ForeColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.labelTitle.Location = new System.Drawing.Point(20, 16);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(400, 32);
            this.labelTitle.TabIndex = 1;
            this.labelTitle.Text = "PADUEX5 固件升级";
            this.labelTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelTips
            // 
            this.labelTips.AutoSize = false;
            this.labelTips.BackColor = System.Drawing.Color.FromArgb(255, 248, 240);
            this.labelTips.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelTips.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTips.ForeColor = System.Drawing.Color.FromArgb(90, 50, 20);
            this.labelTips.Location = new System.Drawing.Point(20, 56);
            this.labelTips.Name = "labelTips";
            this.labelTips.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.labelTips.Size = new System.Drawing.Size(520, 88);
            this.labelTips.TabIndex = 2;
            this.labelTips.Text = "操作提示：\r\n1. 选择 firmware\\grok1.5.bin（同体积；改键；已修 1.2 boot 变砖；勿用 1.2/1.3/1.4）\r\n2. 保持 USB 连接，升级中勿拔线、勿关机、勿关闭本窗口\r\n3. 失败留在 Bootloader 可重试；ISP 请用 grok1.5/1.0（勿用 1.2/1.3/1.4/1.1）";
            // 
            // labelAddr
            // 
            this.labelAddr.AutoSize = false;
            this.labelAddr.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelAddr.Location = new System.Drawing.Point(20, 156);
            this.labelAddr.Name = "labelAddr";
            this.labelAddr.Size = new System.Drawing.Size(250, 22);
            this.labelAddr.TabIndex = 3;
            this.labelAddr.Text = "APP 地址: 0x08004000";
            this.labelAddr.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelLimit
            // 
            this.labelLimit.AutoSize = false;
            this.labelLimit.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelLimit.Location = new System.Drawing.Point(280, 156);
            this.labelLimit.Name = "labelLimit";
            this.labelLimit.Size = new System.Drawing.Size(260, 22);
            this.labelLimit.TabIndex = 4;
            this.labelLimit.Text = "最大允许: 47104 字节（保护配置区）";
            this.labelLimit.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textFile
            // 
            this.textFile.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textFile.Location = new System.Drawing.Point(20, 188);
            this.textFile.Name = "textFile";
            this.textFile.ReadOnly = true;
            this.textFile.Size = new System.Drawing.Size(400, 27);
            this.textFile.TabIndex = 5;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBrowse.Location = new System.Drawing.Point(430, 186);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(110, 32);
            this.btnBrowse.TabIndex = 6;
            this.btnBrowse.Text = "选择固件";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelFileInfo
            // 
            this.labelFileInfo.AutoSize = false;
            this.labelFileInfo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelFileInfo.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);
            this.labelFileInfo.Location = new System.Drawing.Point(20, 224);
            this.labelFileInfo.Name = "labelFileInfo";
            this.labelFileInfo.Size = new System.Drawing.Size(520, 22);
            this.labelFileInfo.TabIndex = 7;
            this.labelFileInfo.Text = "尚未选择文件";
            this.labelFileInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(20, 258);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(450, 28);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.TabIndex = 8;
            // 
            // labelPercent
            // 
            this.labelPercent.AutoSize = false;
            this.labelPercent.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelPercent.Location = new System.Drawing.Point(480, 258);
            this.labelPercent.Name = "labelPercent";
            this.labelPercent.Size = new System.Drawing.Size(60, 28);
            this.labelPercent.TabIndex = 9;
            this.labelPercent.Text = "0%";
            this.labelPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = false;
            this.labelStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelStatus.ForeColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.labelStatus.Location = new System.Drawing.Point(20, 296);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(520, 24);
            this.labelStatus.TabIndex = 10;
            this.labelStatus.Text = "请选择固件 .bin，然后开始升级。";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textLog
            // 
            this.textLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textLog.Location = new System.Drawing.Point(20, 328);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.ReadOnly = true;
            this.textLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLog.Size = new System.Drawing.Size(520, 140);
            this.textLog.TabIndex = 11;
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStart.Location = new System.Drawing.Point(20, 484);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(180, 40);
            this.btnStart.TabIndex = 12;
            this.btnStart.Text = "开始升级";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnClose
            // 
            this.btnClose.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.Location = new System.Drawing.Point(430, 484);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(110, 40);
            this.btnClose.TabIndex = 13;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // App_OTA
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
            this.ClientSize = new System.Drawing.Size(560, 540);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.textLog);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelPercent);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.labelFileInfo);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.textFile);
            this.Controls.Add(this.labelLimit);
            this.Controls.Add(this.labelAddr);
            this.Controls.Add(this.labelTips);
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.panelBanner);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "App_OTA";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "PADUEX5 固件升级";
            this.panelBanner.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Panel panelBanner;
        private System.Windows.Forms.Label labelWarn;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelTips;
        private System.Windows.Forms.Label labelAddr;
        private System.Windows.Forms.Label labelLimit;
        private System.Windows.Forms.TextBox textFile;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelFileInfo;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label labelPercent;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.TextBox textLog;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnClose;
    }
}
