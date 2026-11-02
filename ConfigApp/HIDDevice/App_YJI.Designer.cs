namespace ConfigAppNS
{
    partial class App_YJI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.groupBox_Sensi = new System.Windows.Forms.GroupBox();
            this.Btn_ColorMofy = new System.Windows.Forms.Button();
            this.textBox_vkCode = new System.Windows.Forms.TextBox();
            this.Btn_ADJ = new System.Windows.Forms.Button();
            this.btn_default = new System.Windows.Forms.Button();
            this.Btn_MappingMofy = new System.Windows.Forms.Button();
            this.btn_Save = new System.Windows.Forms.Button();
            this.textBox_Mapping = new System.Windows.Forms.TextBox();
            this.btn_Read = new System.Windows.Forms.Button();
            this.label_NewKey = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.Btn_SaveMode = new System.Windows.Forms.Button();
            this.groupBox_Mode = new System.Windows.Forms.GroupBox();
            this.Btn_ReadMode = new System.Windows.Forms.Button();
            this.groupBox_USBMode = new System.Windows.Forms.GroupBox();
            this.radioButton_KBS = new System.Windows.Forms.RadioButton();
            this.radioButton_HID = new System.Windows.Forms.RadioButton();
            this.radioButton_IO4 = new System.Windows.Forms.RadioButton();
            this.label_Ver = new System.Windows.Forms.Label();
            this.Btn_K1 = new System.Windows.Forms.Button();
            this.Btn_K2 = new System.Windows.Forms.Button();
            this.Btn_K3 = new System.Windows.Forms.Button();
            this.Btn_Left = new System.Windows.Forms.Button();
            this.Btn_K6 = new System.Windows.Forms.Button();
            this.Btn_K5 = new System.Windows.Forms.Button();
            this.Btn_K4 = new System.Windows.Forms.Button();
            this.Btn_Right = new System.Windows.Forms.Button();
            this.Btn_K7 = new System.Windows.Forms.Button();
            this.Btn_K8 = new System.Windows.Forms.Button();
            this.AnalogStick = new System.Windows.Forms.Button();
            this.Btn_KA = new System.Windows.Forms.Button();
            this.Btn_K9 = new System.Windows.Forms.Button();
            this.groupBox_Btn = new System.Windows.Forms.GroupBox();
            this.progressBarStick = new ConfigAppNS.App_YJI.ProgressBarWithValue();
            this.textBoxRight = new System.Windows.Forms.TextBox();
            this.progressBarRight = new ConfigAppNS.App_YJI.ProgressBarWithValue();
            this.trackBar_Rgiht = new System.Windows.Forms.TrackBar();
            this.groupBox_SensiR = new System.Windows.Forms.GroupBox();
            this.groupBox_SensiL = new System.Windows.Forms.GroupBox();
            this.textBoxLeft = new System.Windows.Forms.TextBox();
            this.progressBarLeft = new ConfigAppNS.App_YJI.ProgressBarWithValue();
            this.trackBar_Left = new System.Windows.Forms.TrackBar();
            this.groupBox_Color = new System.Windows.Forms.GroupBox();
            this.Btn_Col_R = new System.Windows.Forms.Button();
            this.Btn_Col_G = new System.Windows.Forms.Button();
            this.Btn_Col_W = new System.Windows.Forms.Button();
            this.Btn_Col_B = new System.Windows.Forms.Button();
            this.Btn_Col_C = new System.Windows.Forms.Button();
            this.Btn_Col_Y = new System.Windows.Forms.Button();
            this.Btn_Col_P = new System.Windows.Forms.Button();
            this.groupBox_Sensi.SuspendLayout();
            this.groupBox_Mode.SuspendLayout();
            this.groupBox_USBMode.SuspendLayout();
            this.groupBox_Btn.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Rgiht)).BeginInit();
            this.groupBox_SensiR.SuspendLayout();
            this.groupBox_SensiL.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Left)).BeginInit();
            this.groupBox_Color.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox_Sensi
            // 
            this.groupBox_Sensi.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.groupBox_Sensi.Controls.Add(this.Btn_ColorMofy);
            this.groupBox_Sensi.Controls.Add(this.textBox_vkCode);
            this.groupBox_Sensi.Controls.Add(this.Btn_ADJ);
            this.groupBox_Sensi.Controls.Add(this.btn_default);
            this.groupBox_Sensi.Controls.Add(this.Btn_MappingMofy);
            this.groupBox_Sensi.Controls.Add(this.btn_Save);
            this.groupBox_Sensi.Controls.Add(this.textBox_Mapping);
            this.groupBox_Sensi.Controls.Add(this.btn_Read);
            this.groupBox_Sensi.Controls.Add(this.label_NewKey);
            this.groupBox_Sensi.Location = new System.Drawing.Point(681, 424);
            this.groupBox_Sensi.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Sensi.Name = "groupBox_Sensi";
            this.groupBox_Sensi.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Sensi.Size = new System.Drawing.Size(990, 150);
            this.groupBox_Sensi.TabIndex = 175;
            this.groupBox_Sensi.TabStop = false;
            this.groupBox_Sensi.Text = "Senitivity";
            // 
            // Btn_ColorMofy
            // 
            this.Btn_ColorMofy.BackColor = System.Drawing.SystemColors.Control;
            this.Btn_ColorMofy.Location = new System.Drawing.Point(546, 90);
            this.Btn_ColorMofy.Name = "Btn_ColorMofy";
            this.Btn_ColorMofy.Size = new System.Drawing.Size(140, 48);
            this.Btn_ColorMofy.TabIndex = 173;
            this.Btn_ColorMofy.Text = "灯光颜色";
            this.Btn_ColorMofy.UseVisualStyleBackColor = false;
            this.Btn_ColorMofy.Click += new System.EventHandler(this.Btn_ColorMofy_Click);
            // 
            // textBox_vkCode
            // 
            this.textBox_vkCode.Enabled = false;
            this.textBox_vkCode.Font = new System.Drawing.Font("SimSun", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox_vkCode.Location = new System.Drawing.Point(766, 38);
            this.textBox_vkCode.MaxLength = 1;
            this.textBox_vkCode.Name = "textBox_vkCode";
            this.textBox_vkCode.ReadOnly = true;
            this.textBox_vkCode.Size = new System.Drawing.Size(61, 32);
            this.textBox_vkCode.TabIndex = 172;
            this.textBox_vkCode.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // Btn_ADJ
            // 
            this.Btn_ADJ.BackColor = System.Drawing.SystemColors.Control;
            this.Btn_ADJ.Location = new System.Drawing.Point(44, 90);
            this.Btn_ADJ.Name = "Btn_ADJ";
            this.Btn_ADJ.Size = new System.Drawing.Size(147, 48);
            this.Btn_ADJ.TabIndex = 134;
            this.Btn_ADJ.Text = "调试模式";
            this.Btn_ADJ.UseVisualStyleBackColor = false;
            this.Btn_ADJ.Click += new System.EventHandler(this.Btn_ADJ_Click);
            // 
            // btn_default
            // 
            this.btn_default.BackColor = System.Drawing.SystemColors.Control;
            this.btn_default.Location = new System.Drawing.Point(44, 30);
            this.btn_default.Name = "btn_default";
            this.btn_default.Size = new System.Drawing.Size(147, 48);
            this.btn_default.TabIndex = 109;
            this.btn_default.Text = "默认设置";
            this.btn_default.UseVisualStyleBackColor = false;
            this.btn_default.Click += new System.EventHandler(this.btn_default_Click);
            // 
            // Btn_MappingMofy
            // 
            this.Btn_MappingMofy.BackColor = System.Drawing.SystemColors.Control;
            this.Btn_MappingMofy.Location = new System.Drawing.Point(546, 30);
            this.Btn_MappingMofy.Name = "Btn_MappingMofy";
            this.Btn_MappingMofy.Size = new System.Drawing.Size(140, 48);
            this.Btn_MappingMofy.TabIndex = 167;
            this.Btn_MappingMofy.Text = "键值修改";
            this.Btn_MappingMofy.UseVisualStyleBackColor = false;
            this.Btn_MappingMofy.Click += new System.EventHandler(this.Btn_MappingMofy_Click);
            // 
            // btn_Save
            // 
            this.btn_Save.BackColor = System.Drawing.SystemColors.Control;
            this.btn_Save.Location = new System.Drawing.Point(360, 30);
            this.btn_Save.Name = "btn_Save";
            this.btn_Save.Size = new System.Drawing.Size(147, 48);
            this.btn_Save.TabIndex = 111;
            this.btn_Save.Text = "保存设置";
            this.btn_Save.UseVisualStyleBackColor = false;
            this.btn_Save.Click += new System.EventHandler(this.btn_Save_Click);
            // 
            // textBox_Mapping
            // 
            this.textBox_Mapping.Enabled = false;
            this.textBox_Mapping.Font = new System.Drawing.Font("SimSun", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox_Mapping.Location = new System.Drawing.Point(834, 38);
            this.textBox_Mapping.MaxLength = 1;
            this.textBox_Mapping.Name = "textBox_Mapping";
            this.textBox_Mapping.ReadOnly = true;
            this.textBox_Mapping.Size = new System.Drawing.Size(126, 32);
            this.textBox_Mapping.TabIndex = 169;
            this.textBox_Mapping.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btn_Read
            // 
            this.btn_Read.BackColor = System.Drawing.SystemColors.Control;
            this.btn_Read.Location = new System.Drawing.Point(202, 30);
            this.btn_Read.Name = "btn_Read";
            this.btn_Read.Size = new System.Drawing.Size(147, 48);
            this.btn_Read.TabIndex = 110;
            this.btn_Read.Text = "读取设置";
            this.btn_Read.UseVisualStyleBackColor = false;
            this.btn_Read.Click += new System.EventHandler(this.btn_Read_Click);
            // 
            // label_NewKey
            // 
            this.label_NewKey.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.label_NewKey.Enabled = false;
            this.label_NewKey.Font = new System.Drawing.Font("SimSun", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label_NewKey.Location = new System.Drawing.Point(688, 28);
            this.label_NewKey.Name = "label_NewKey";
            this.label_NewKey.Size = new System.Drawing.Size(88, 51);
            this.label_NewKey.TabIndex = 168;
            this.label_NewKey.Text = "新键值:";
            this.label_NewKey.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // timer1
            // 
            this.timer1.Interval = 300;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // Btn_SaveMode
            // 
            this.Btn_SaveMode.BackColor = System.Drawing.SystemColors.Control;
            this.Btn_SaveMode.Location = new System.Drawing.Point(243, 86);
            this.Btn_SaveMode.Name = "Btn_SaveMode";
            this.Btn_SaveMode.Size = new System.Drawing.Size(147, 48);
            this.Btn_SaveMode.TabIndex = 175;
            this.Btn_SaveMode.Text = "保存模式";
            this.Btn_SaveMode.UseVisualStyleBackColor = false;
            this.Btn_SaveMode.Click += new System.EventHandler(this.Btn_SaveMode_Click);
            // 
            // groupBox_Mode
            // 
            this.groupBox_Mode.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.groupBox_Mode.Controls.Add(this.Btn_SaveMode);
            this.groupBox_Mode.Controls.Add(this.Btn_ReadMode);
            this.groupBox_Mode.Controls.Add(this.groupBox_USBMode);
            this.groupBox_Mode.Location = new System.Drawing.Point(234, 424);
            this.groupBox_Mode.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Mode.Name = "groupBox_Mode";
            this.groupBox_Mode.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Mode.Size = new System.Drawing.Size(417, 150);
            this.groupBox_Mode.TabIndex = 188;
            this.groupBox_Mode.TabStop = false;
            this.groupBox_Mode.Text = "Mode";
            // 
            // Btn_ReadMode
            // 
            this.Btn_ReadMode.BackColor = System.Drawing.SystemColors.Control;
            this.Btn_ReadMode.Location = new System.Drawing.Point(30, 86);
            this.Btn_ReadMode.Name = "Btn_ReadMode";
            this.Btn_ReadMode.Size = new System.Drawing.Size(147, 48);
            this.Btn_ReadMode.TabIndex = 174;
            this.Btn_ReadMode.Text = "读取模式";
            this.Btn_ReadMode.UseVisualStyleBackColor = false;
            this.Btn_ReadMode.Click += new System.EventHandler(this.Btn_ReadMode_Click);
            // 
            // groupBox_USBMode
            // 
            this.groupBox_USBMode.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.groupBox_USBMode.Controls.Add(this.radioButton_KBS);
            this.groupBox_USBMode.Controls.Add(this.radioButton_HID);
            this.groupBox_USBMode.Controls.Add(this.radioButton_IO4);
            this.groupBox_USBMode.Location = new System.Drawing.Point(36, 18);
            this.groupBox_USBMode.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_USBMode.Name = "groupBox_USBMode";
            this.groupBox_USBMode.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_USBMode.Size = new System.Drawing.Size(354, 60);
            this.groupBox_USBMode.TabIndex = 172;
            this.groupBox_USBMode.TabStop = false;
            this.groupBox_USBMode.Text = "USB";
            // 
            // radioButton_KBS
            // 
            this.radioButton_KBS.AutoSize = true;
            this.radioButton_KBS.Checked = true;
            this.radioButton_KBS.Location = new System.Drawing.Point(21, 24);
            this.radioButton_KBS.Name = "radioButton_KBS";
            this.radioButton_KBS.Size = new System.Drawing.Size(105, 22);
            this.radioButton_KBS.TabIndex = 167;
            this.radioButton_KBS.TabStop = true;
            this.radioButton_KBS.Text = "KeyBoard";
            this.radioButton_KBS.UseVisualStyleBackColor = true;
            // 
            // radioButton_HID
            // 
            this.radioButton_HID.AutoSize = true;
            this.radioButton_HID.Location = new System.Drawing.Point(134, 24);
            this.radioButton_HID.Name = "radioButton_HID";
            this.radioButton_HID.Size = new System.Drawing.Size(105, 22);
            this.radioButton_HID.TabIndex = 168;
            this.radioButton_HID.TabStop = true;
            this.radioButton_HID.Text = "HID Mode";
            this.radioButton_HID.UseVisualStyleBackColor = true;
            // 
            // radioButton_IO4
            // 
            this.radioButton_IO4.AutoSize = true;
            this.radioButton_IO4.Location = new System.Drawing.Point(246, 24);
            this.radioButton_IO4.Name = "radioButton_IO4";
            this.radioButton_IO4.Size = new System.Drawing.Size(96, 22);
            this.radioButton_IO4.TabIndex = 169;
            this.radioButton_IO4.TabStop = true;
            this.radioButton_IO4.Text = "IO Mode";
            this.radioButton_IO4.UseVisualStyleBackColor = true;
            // 
            // label_Ver
            // 
            this.label_Ver.AutoSize = true;
            this.label_Ver.Location = new System.Drawing.Point(268, 591);
            this.label_Ver.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label_Ver.Name = "label_Ver";
            this.label_Ver.Size = new System.Drawing.Size(44, 18);
            this.label_Ver.TabIndex = 189;
            this.label_Ver.Text = "Ver:";
            // 
            // Btn_K1
            // 
            this.Btn_K1.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K1.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K1.Location = new System.Drawing.Point(204, 225);
            this.Btn_K1.Name = "Btn_K1";
            this.Btn_K1.Size = new System.Drawing.Size(120, 120);
            this.Btn_K1.TabIndex = 190;
            this.Btn_K1.Text = "S";
            this.Btn_K1.UseVisualStyleBackColor = false;
            this.Btn_K1.Click += new System.EventHandler(this.Btn_K1_Click_1);
            // 
            // Btn_K2
            // 
            this.Btn_K2.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K2.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K2.Location = new System.Drawing.Point(370, 225);
            this.Btn_K2.Name = "Btn_K2";
            this.Btn_K2.Size = new System.Drawing.Size(120, 120);
            this.Btn_K2.TabIndex = 191;
            this.Btn_K2.Text = "D";
            this.Btn_K2.UseVisualStyleBackColor = false;
            this.Btn_K2.Click += new System.EventHandler(this.Btn_K2_Click_1);
            // 
            // Btn_K3
            // 
            this.Btn_K3.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K3.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K3.Location = new System.Drawing.Point(537, 225);
            this.Btn_K3.Name = "Btn_K3";
            this.Btn_K3.Size = new System.Drawing.Size(120, 120);
            this.Btn_K3.TabIndex = 192;
            this.Btn_K3.Text = "F";
            this.Btn_K3.UseVisualStyleBackColor = false;
            this.Btn_K3.Click += new System.EventHandler(this.Btn_K3_Click_1);
            // 
            // Btn_Left
            // 
            this.Btn_Left.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_Left.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Left.Location = new System.Drawing.Point(9, 76);
            this.Btn_Left.Name = "Btn_Left";
            this.Btn_Left.Size = new System.Drawing.Size(165, 268);
            this.Btn_Left.TabIndex = 193;
            this.Btn_Left.Text = "L";
            this.Btn_Left.UseVisualStyleBackColor = false;
            this.Btn_Left.Click += new System.EventHandler(this.Btn_Left_Click);
            // 
            // Btn_K6
            // 
            this.Btn_K6.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K6.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K6.Location = new System.Drawing.Point(1173, 225);
            this.Btn_K6.Name = "Btn_K6";
            this.Btn_K6.Size = new System.Drawing.Size(120, 120);
            this.Btn_K6.TabIndex = 196;
            this.Btn_K6.Text = "L";
            this.Btn_K6.UseVisualStyleBackColor = false;
            this.Btn_K6.Click += new System.EventHandler(this.Btn_K6_Click);
            // 
            // Btn_K5
            // 
            this.Btn_K5.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K5.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K5.Location = new System.Drawing.Point(1006, 225);
            this.Btn_K5.Name = "Btn_K5";
            this.Btn_K5.Size = new System.Drawing.Size(120, 120);
            this.Btn_K5.TabIndex = 195;
            this.Btn_K5.Text = "K";
            this.Btn_K5.UseVisualStyleBackColor = false;
            this.Btn_K5.Click += new System.EventHandler(this.Btn_K5_Click);
            // 
            // Btn_K4
            // 
            this.Btn_K4.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K4.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K4.Location = new System.Drawing.Point(840, 225);
            this.Btn_K4.Name = "Btn_K4";
            this.Btn_K4.Size = new System.Drawing.Size(120, 120);
            this.Btn_K4.TabIndex = 194;
            this.Btn_K4.Text = "J";
            this.Btn_K4.UseVisualStyleBackColor = false;
            this.Btn_K4.Click += new System.EventHandler(this.Btn_K4_Click_1);
            // 
            // Btn_Right
            // 
            this.Btn_Right.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_Right.Font = new System.Drawing.Font("SimSun", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Right.Location = new System.Drawing.Point(1336, 76);
            this.Btn_Right.Name = "Btn_Right";
            this.Btn_Right.Size = new System.Drawing.Size(165, 268);
            this.Btn_Right.TabIndex = 197;
            this.Btn_Right.Text = "R";
            this.Btn_Right.UseVisualStyleBackColor = false;
            this.Btn_Right.Click += new System.EventHandler(this.Btn_Right_Click);
            // 
            // Btn_K7
            // 
            this.Btn_K7.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K7.Font = new System.Drawing.Font("SimSun", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K7.Location = new System.Drawing.Point(249, 86);
            this.Btn_K7.Name = "Btn_K7";
            this.Btn_K7.Size = new System.Drawing.Size(120, 120);
            this.Btn_K7.TabIndex = 198;
            this.Btn_K7.Text = "Space";
            this.Btn_K7.UseVisualStyleBackColor = false;
            this.Btn_K7.Click += new System.EventHandler(this.Btn_K7_Click);
            // 
            // Btn_K8
            // 
            this.Btn_K8.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K8.Font = new System.Drawing.Font("SimSun", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K8.Location = new System.Drawing.Point(1136, 86);
            this.Btn_K8.Name = "Btn_K8";
            this.Btn_K8.Size = new System.Drawing.Size(120, 120);
            this.Btn_K8.TabIndex = 199;
            this.Btn_K8.Text = "Enter";
            this.Btn_K8.UseVisualStyleBackColor = false;
            this.Btn_K8.Click += new System.EventHandler(this.Btn_K8_Click);
            // 
            // AnalogStick
            // 
            this.AnalogStick.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.AnalogStick.Font = new System.Drawing.Font("SimSun", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.AnalogStick.Location = new System.Drawing.Point(696, 76);
            this.AnalogStick.Name = "AnalogStick";
            this.AnalogStick.Size = new System.Drawing.Size(120, 120);
            this.AnalogStick.TabIndex = 200;
            this.AnalogStick.Text = "Stick";
            this.AnalogStick.UseVisualStyleBackColor = false;
            // 
            // Btn_KA
            // 
            this.Btn_KA.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_KA.Font = new System.Drawing.Font("SimSun", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_KA.Location = new System.Drawing.Point(1134, 14);
            this.Btn_KA.Name = "Btn_KA";
            this.Btn_KA.Size = new System.Drawing.Size(120, 60);
            this.Btn_KA.TabIndex = 202;
            this.Btn_KA.Text = "SW2";
            this.Btn_KA.UseVisualStyleBackColor = false;
            this.Btn_KA.Click += new System.EventHandler(this.Btn_KA_Click);
            // 
            // Btn_K9
            // 
            this.Btn_K9.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Btn_K9.Font = new System.Drawing.Font("SimSun", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_K9.Location = new System.Drawing.Point(1008, 14);
            this.Btn_K9.Name = "Btn_K9";
            this.Btn_K9.Size = new System.Drawing.Size(120, 60);
            this.Btn_K9.TabIndex = 201;
            this.Btn_K9.Text = "SW1";
            this.Btn_K9.UseVisualStyleBackColor = false;
            this.Btn_K9.Click += new System.EventHandler(this.Btn_K9_Click);
            // 
            // groupBox_Btn
            // 
            this.groupBox_Btn.Controls.Add(this.progressBarStick);
            this.groupBox_Btn.Controls.Add(this.Btn_K3);
            this.groupBox_Btn.Controls.Add(this.Btn_KA);
            this.groupBox_Btn.Controls.Add(this.Btn_K1);
            this.groupBox_Btn.Controls.Add(this.Btn_K9);
            this.groupBox_Btn.Controls.Add(this.Btn_Right);
            this.groupBox_Btn.Controls.Add(this.Btn_K2);
            this.groupBox_Btn.Controls.Add(this.Btn_Left);
            this.groupBox_Btn.Controls.Add(this.AnalogStick);
            this.groupBox_Btn.Controls.Add(this.Btn_K4);
            this.groupBox_Btn.Controls.Add(this.Btn_K8);
            this.groupBox_Btn.Controls.Add(this.Btn_K5);
            this.groupBox_Btn.Controls.Add(this.Btn_K7);
            this.groupBox_Btn.Controls.Add(this.Btn_K6);
            this.groupBox_Btn.Location = new System.Drawing.Point(171, 10);
            this.groupBox_Btn.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Btn.Name = "groupBox_Btn";
            this.groupBox_Btn.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_Btn.Size = new System.Drawing.Size(1512, 370);
            this.groupBox_Btn.TabIndex = 203;
            this.groupBox_Btn.TabStop = false;
            this.groupBox_Btn.Text = "Button";
            // 
            // progressBarStick
            // 
            this.progressBarStick.CustomText = null;
            this.progressBarStick.Direction = ConfigAppNS.App_YJI.ProgressBarDirection.Horizontal;
            this.progressBarStick.Location = new System.Drawing.Point(597, 20);
            this.progressBarStick.Maximum = 4095;
            this.progressBarStick.Name = "progressBarStick";
            this.progressBarStick.ShowPercent = true;
            this.progressBarStick.Size = new System.Drawing.Size(321, 45);
            this.progressBarStick.Step = 1;
            this.progressBarStick.TabIndex = 208;
            // 
            // textBoxRight
            // 
            this.textBoxRight.Font = new System.Drawing.Font("SimSun", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBoxRight.Location = new System.Drawing.Point(32, 555);
            this.textBoxRight.MaxLength = 4;
            this.textBoxRight.Name = "textBoxRight";
            this.textBoxRight.Size = new System.Drawing.Size(68, 35);
            this.textBoxRight.TabIndex = 209;
            this.textBoxRight.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBoxRight.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // progressBarRight
            // 
            this.progressBarRight.CustomText = null;
            this.progressBarRight.Direction = ConfigAppNS.App_YJI.ProgressBarDirection.Horizontal;
            this.progressBarRight.Location = new System.Drawing.Point(45, 50);
            this.progressBarRight.Maximum = 4095;
            this.progressBarRight.Name = "progressBarRight";
            this.progressBarRight.ShowPercent = true;
            this.progressBarRight.Size = new System.Drawing.Size(57, 480);
            this.progressBarRight.Step = 1;
            this.progressBarRight.TabIndex = 207;
            // 
            // trackBar_Rgiht
            // 
            this.trackBar_Rgiht.Location = new System.Drawing.Point(14, 30);
            this.trackBar_Rgiht.Maximum = 4095;
            this.trackBar_Rgiht.Name = "trackBar_Rgiht";
            this.trackBar_Rgiht.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.trackBar_Rgiht.Size = new System.Drawing.Size(69, 520);
            this.trackBar_Rgiht.TabIndex = 208;
            this.trackBar_Rgiht.Scroll += new System.EventHandler(this.trackBar2_Scroll);
            // 
            // groupBox_SensiR
            // 
            this.groupBox_SensiR.Controls.Add(this.textBoxRight);
            this.groupBox_SensiR.Controls.Add(this.progressBarRight);
            this.groupBox_SensiR.Controls.Add(this.trackBar_Rgiht);
            this.groupBox_SensiR.Location = new System.Drawing.Point(1725, 15);
            this.groupBox_SensiR.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_SensiR.Name = "groupBox_SensiR";
            this.groupBox_SensiR.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_SensiR.Size = new System.Drawing.Size(129, 621);
            this.groupBox_SensiR.TabIndex = 210;
            this.groupBox_SensiR.TabStop = false;
            this.groupBox_SensiR.Text = "Sensi-R";
            // 
            // groupBox_SensiL
            // 
            this.groupBox_SensiL.Controls.Add(this.textBoxLeft);
            this.groupBox_SensiL.Controls.Add(this.progressBarLeft);
            this.groupBox_SensiL.Controls.Add(this.trackBar_Left);
            this.groupBox_SensiL.Location = new System.Drawing.Point(15, 15);
            this.groupBox_SensiL.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_SensiL.Name = "groupBox_SensiL";
            this.groupBox_SensiL.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox_SensiL.Size = new System.Drawing.Size(129, 621);
            this.groupBox_SensiL.TabIndex = 211;
            this.groupBox_SensiL.TabStop = false;
            this.groupBox_SensiL.Text = "Sensi-L";
            // 
            // textBoxLeft
            // 
            this.textBoxLeft.Font = new System.Drawing.Font("SimSun", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBoxLeft.Location = new System.Drawing.Point(32, 555);
            this.textBoxLeft.MaxLength = 4;
            this.textBoxLeft.Name = "textBoxLeft";
            this.textBoxLeft.Size = new System.Drawing.Size(68, 35);
            this.textBoxLeft.TabIndex = 209;
            this.textBoxLeft.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBoxLeft.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // progressBarLeft
            // 
            this.progressBarLeft.CustomText = null;
            this.progressBarLeft.Direction = ConfigAppNS.App_YJI.ProgressBarDirection.Horizontal;
            this.progressBarLeft.Location = new System.Drawing.Point(45, 50);
            this.progressBarLeft.Maximum = 4095;
            this.progressBarLeft.Name = "progressBarLeft";
            this.progressBarLeft.ShowPercent = true;
            this.progressBarLeft.Size = new System.Drawing.Size(57, 480);
            this.progressBarLeft.Step = 1;
            this.progressBarLeft.TabIndex = 207;
            // 
            // trackBar_Left
            // 
            this.trackBar_Left.Location = new System.Drawing.Point(14, 30);
            this.trackBar_Left.Maximum = 4095;
            this.trackBar_Left.Name = "trackBar_Left";
            this.trackBar_Left.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.trackBar_Left.Size = new System.Drawing.Size(69, 520);
            this.trackBar_Left.TabIndex = 208;
            this.trackBar_Left.Scroll += new System.EventHandler(this.trackBar1_Scroll);
            // 
            // groupBox_Color
            // 
            this.groupBox_Color.Controls.Add(this.Btn_Col_R);
            this.groupBox_Color.Controls.Add(this.Btn_Col_G);
            this.groupBox_Color.Controls.Add(this.Btn_Col_W);
            this.groupBox_Color.Controls.Add(this.Btn_Col_B);
            this.groupBox_Color.Controls.Add(this.Btn_Col_C);
            this.groupBox_Color.Controls.Add(this.Btn_Col_Y);
            this.groupBox_Color.Controls.Add(this.Btn_Col_P);
            this.groupBox_Color.Location = new System.Drawing.Point(952, 582);
            this.groupBox_Color.Name = "groupBox_Color";
            this.groupBox_Color.Size = new System.Drawing.Size(720, 105);
            this.groupBox_Color.TabIndex = 212;
            this.groupBox_Color.TabStop = false;
            this.groupBox_Color.Text = "Color";
            this.groupBox_Color.Visible = false;
            // 
            // Btn_Col_R
            // 
            this.Btn_Col_R.BackColor = System.Drawing.Color.Red;
            this.Btn_Col_R.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_R.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_R.Location = new System.Drawing.Point(74, 32);
            this.Btn_Col_R.Name = "Btn_Col_R";
            this.Btn_Col_R.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_R.TabIndex = 123;
            this.Btn_Col_R.UseVisualStyleBackColor = false;
            this.Btn_Col_R.Click += new System.EventHandler(this.Btn_Col_R_Click);
            // 
            // Btn_Col_G
            // 
            this.Btn_Col_G.BackColor = System.Drawing.Color.Green;
            this.Btn_Col_G.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_G.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_G.Location = new System.Drawing.Point(165, 32);
            this.Btn_Col_G.Name = "Btn_Col_G";
            this.Btn_Col_G.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_G.TabIndex = 124;
            this.Btn_Col_G.UseVisualStyleBackColor = false;
            this.Btn_Col_G.Click += new System.EventHandler(this.Btn_Col_G_Click);
            // 
            // Btn_Col_W
            // 
            this.Btn_Col_W.BackColor = System.Drawing.Color.White;
            this.Btn_Col_W.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_W.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_W.Location = new System.Drawing.Point(627, 32);
            this.Btn_Col_W.Name = "Btn_Col_W";
            this.Btn_Col_W.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_W.TabIndex = 129;
            this.Btn_Col_W.UseVisualStyleBackColor = false;
            this.Btn_Col_W.Click += new System.EventHandler(this.Btn_Col_W_Click);
            // 
            // Btn_Col_B
            // 
            this.Btn_Col_B.BackColor = System.Drawing.Color.Blue;
            this.Btn_Col_B.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_B.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_B.Location = new System.Drawing.Point(258, 32);
            this.Btn_Col_B.Name = "Btn_Col_B";
            this.Btn_Col_B.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_B.TabIndex = 125;
            this.Btn_Col_B.UseVisualStyleBackColor = false;
            this.Btn_Col_B.Click += new System.EventHandler(this.Btn_Col_B_Click);
            // 
            // Btn_Col_C
            // 
            this.Btn_Col_C.BackColor = System.Drawing.Color.Cyan;
            this.Btn_Col_C.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_C.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_C.Location = new System.Drawing.Point(534, 32);
            this.Btn_Col_C.Name = "Btn_Col_C";
            this.Btn_Col_C.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_C.TabIndex = 128;
            this.Btn_Col_C.UseVisualStyleBackColor = false;
            this.Btn_Col_C.Click += new System.EventHandler(this.Btn_Col_C_Click);
            // 
            // Btn_Col_Y
            // 
            this.Btn_Col_Y.BackColor = System.Drawing.Color.Yellow;
            this.Btn_Col_Y.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_Y.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_Y.Location = new System.Drawing.Point(350, 32);
            this.Btn_Col_Y.Name = "Btn_Col_Y";
            this.Btn_Col_Y.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_Y.TabIndex = 126;
            this.Btn_Col_Y.UseVisualStyleBackColor = false;
            this.Btn_Col_Y.Click += new System.EventHandler(this.Btn_Col_Y_Click);
            // 
            // Btn_Col_P
            // 
            this.Btn_Col_P.BackColor = System.Drawing.Color.Fuchsia;
            this.Btn_Col_P.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn_Col_P.Font = new System.Drawing.Font("SimSun", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Btn_Col_P.Location = new System.Drawing.Point(442, 32);
            this.Btn_Col_P.Name = "Btn_Col_P";
            this.Btn_Col_P.Size = new System.Drawing.Size(57, 60);
            this.Btn_Col_P.TabIndex = 127;
            this.Btn_Col_P.UseVisualStyleBackColor = false;
            this.Btn_Col_P.Click += new System.EventHandler(this.Btn_Col_P_Click);
            // 
            // App_YJI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1887, 704);
            this.Controls.Add(this.groupBox_Color);
            this.Controls.Add(this.groupBox_SensiL);
            this.Controls.Add(this.groupBox_SensiR);
            this.Controls.Add(this.groupBox_Btn);
            this.Controls.Add(this.groupBox_Sensi);
            this.Controls.Add(this.label_Ver);
            this.Controls.Add(this.groupBox_Mode);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "App_YJI";
            this.Text = "Form1";
            this.groupBox_Sensi.ResumeLayout(false);
            this.groupBox_Sensi.PerformLayout();
            this.groupBox_Mode.ResumeLayout(false);
            this.groupBox_USBMode.ResumeLayout(false);
            this.groupBox_USBMode.PerformLayout();
            this.groupBox_Btn.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Rgiht)).EndInit();
            this.groupBox_SensiR.ResumeLayout(false);
            this.groupBox_SensiR.PerformLayout();
            this.groupBox_SensiL.ResumeLayout(false);
            this.groupBox_SensiL.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Left)).EndInit();
            this.groupBox_Color.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox_Sensi;
        private System.Windows.Forms.Button Btn_ADJ;
        private System.Windows.Forms.Button btn_default;
        private System.Windows.Forms.Button btn_Save;
        private System.Windows.Forms.Button btn_Read;
        public System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button Btn_SaveMode;
        private System.Windows.Forms.GroupBox groupBox_Mode;
        private System.Windows.Forms.Button Btn_ReadMode;
        private System.Windows.Forms.GroupBox groupBox_USBMode;
        private System.Windows.Forms.RadioButton radioButton_KBS;
        private System.Windows.Forms.RadioButton radioButton_HID;
        private System.Windows.Forms.RadioButton radioButton_IO4;
        private System.Windows.Forms.Label label_Ver;
        private System.Windows.Forms.Button Btn_K1;
        private System.Windows.Forms.Button Btn_K2;
        private System.Windows.Forms.Button Btn_K3;
        private System.Windows.Forms.Button Btn_Left;
        private System.Windows.Forms.Button Btn_K6;
        private System.Windows.Forms.Button Btn_K5;
        private System.Windows.Forms.Button Btn_K4;
        private System.Windows.Forms.Button Btn_Right;
        private System.Windows.Forms.Button Btn_K7;
        private System.Windows.Forms.Button Btn_K8;
        private System.Windows.Forms.Button AnalogStick;
        private System.Windows.Forms.Button Btn_KA;
        private System.Windows.Forms.Button Btn_K9;
        private System.Windows.Forms.GroupBox groupBox_Btn;
        private System.Windows.Forms.TextBox textBoxRight;
        private App_YJI.ProgressBarWithValue progressBarRight;
        private System.Windows.Forms.TrackBar trackBar_Rgiht;
        private System.Windows.Forms.GroupBox groupBox_SensiR;
        private System.Windows.Forms.GroupBox groupBox_SensiL;
        private System.Windows.Forms.TextBox textBoxLeft;
        private App_YJI.ProgressBarWithValue progressBarLeft;
        private System.Windows.Forms.TrackBar trackBar_Left;
        private System.Windows.Forms.TextBox textBox_vkCode;
        private System.Windows.Forms.Button Btn_MappingMofy;
        private System.Windows.Forms.TextBox textBox_Mapping;
        private System.Windows.Forms.Label label_NewKey;
        private App_YJI.ProgressBarWithValue progressBarStick;
        private System.Windows.Forms.Button Btn_ColorMofy;
        private System.Windows.Forms.GroupBox groupBox_Color;
        private System.Windows.Forms.Button Btn_Col_R;
        private System.Windows.Forms.Button Btn_Col_G;
        private System.Windows.Forms.Button Btn_Col_W;
        private System.Windows.Forms.Button Btn_Col_B;
        private System.Windows.Forms.Button Btn_Col_C;
        private System.Windows.Forms.Button Btn_Col_Y;
        private System.Windows.Forms.Button Btn_Col_P;
    }
}