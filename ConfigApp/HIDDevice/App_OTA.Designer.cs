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
            this.labelTitle = new System.Windows.Forms.Label();
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
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 16F, System.Drawing.FontStyle.Bold);
            this.labelTitle.Location = new System.Drawing.Point(24, 18);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(220, 36);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "PADUEX5 固件升级";
            // 
            // labelAddr
            // 
            this.labelAddr.AutoSize = true;
            this.labelAddr.Location = new System.Drawing.Point(28, 68);
            this.labelAddr.Name = "labelAddr";
            this.labelAddr.Size = new System.Drawing.Size(161, 18);
            this.labelAddr.TabIndex = 1;
            this.labelAddr.Text = "APP 地址: 0x08004000";
            // 
            // labelLimit
            // 
            this.labelLimit.AutoSize = true;
            this.labelLimit.Location = new System.Drawing.Point(280, 68);
            this.labelLimit.Name = "labelLimit";
            this.labelLimit.Size = new System.Drawing.Size(197, 18);
            this.labelLimit.TabIndex = 2;
            this.labelLimit.Text = "最大允许: 46080 字节";
            // 
            // textFile
            // 
            this.textFile.Location = new System.Drawing.Point(31, 108);
            this.textFile.Name = "textFile";
            this.textFile.ReadOnly = true;
            this.textFile.Size = new System.Drawing.Size(520, 28);
            this.textFile.TabIndex = 3;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(568, 104);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(120, 36);
            this.btnBrowse.TabIndex = 4;
            this.btnBrowse.Text = "选择固件";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelFileInfo
            // 
            this.labelFileInfo.AutoSize = true;
            this.labelFileInfo.Location = new System.Drawing.Point(28, 150);
            this.labelFileInfo.Name = "labelFileInfo";
            this.labelFileInfo.Size = new System.Drawing.Size(98, 18);
            this.labelFileInfo.TabIndex = 5;
            this.labelFileInfo.Text = "尚未选择文件";
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(31, 190);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(580, 32);
            this.progressBar.TabIndex = 6;
            // 
            // labelPercent
            // 
            this.labelPercent.AutoSize = true;
            this.labelPercent.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelPercent.Location = new System.Drawing.Point(628, 192);
            this.labelPercent.Name = "labelPercent";
            this.labelPercent.Size = new System.Drawing.Size(50, 27);
            this.labelPercent.TabIndex = 7;
            this.labelPercent.Text = "0%";
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(28, 240);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(80, 18);
            this.labelStatus.TabIndex = 8;
            this.labelStatus.Text = "就绪";
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(31, 275);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.ReadOnly = true;
            this.textLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLog.Size = new System.Drawing.Size(657, 220);
            this.textLog.TabIndex = 9;
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.Location = new System.Drawing.Point(31, 520);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(200, 48);
            this.btnStart.TabIndex = 10;
            this.btnStart.Text = "开始升级";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(568, 520);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(120, 48);
            this.btnClose.TabIndex = 11;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // App_OTA
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(720, 592);
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
            this.Controls.Add(this.labelTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "App_OTA";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "PADUEX5 Firmware Upgrade";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelTitle;
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
