namespace ConfigAppNS
{
    partial class App_Main
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
            this.Btn_ADX = new System.Windows.Forms.Button();
            this.Btn_DDR = new System.Windows.Forms.Button();
            this.Btn_ZER = new System.Windows.Forms.Button();
            this.Btn_YJI = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // Btn_ADX
            // 
            this.Btn_ADX.Location = new System.Drawing.Point(204, 147);
            this.Btn_ADX.Name = "Btn_ADX";
            this.Btn_ADX.Size = new System.Drawing.Size(135, 66);
            this.Btn_ADX.TabIndex = 0;
            this.Btn_ADX.Text = "ADX";
            this.Btn_ADX.UseVisualStyleBackColor = true;
            this.Btn_ADX.Click += new System.EventHandler(this.Btn_ADX_Click);
            // 
            // Btn_DDR
            // 
            this.Btn_DDR.Location = new System.Drawing.Point(204, 256);
            this.Btn_DDR.Name = "Btn_DDR";
            this.Btn_DDR.Size = new System.Drawing.Size(135, 66);
            this.Btn_DDR.TabIndex = 1;
            this.Btn_DDR.Text = "Yuanpad 5Key";
            this.Btn_DDR.UseVisualStyleBackColor = true;
            this.Btn_DDR.Click += new System.EventHandler(this.Btn_DDR_Click);
            // 
            // Btn_ZER
            // 
            this.Btn_ZER.Location = new System.Drawing.Point(204, 366);
            this.Btn_ZER.Name = "Btn_ZER";
            this.Btn_ZER.Size = new System.Drawing.Size(135, 66);
            this.Btn_ZER.TabIndex = 2;
            this.Btn_ZER.Text = "Laverita";
            this.Btn_ZER.UseVisualStyleBackColor = true;
            this.Btn_ZER.Click += new System.EventHandler(this.Btn_ZER_Click);
            // 
            // Btn_YJI
            // 
            this.Btn_YJI.Location = new System.Drawing.Point(204, 476);
            this.Btn_YJI.Name = "Btn_YJI";
            this.Btn_YJI.Size = new System.Drawing.Size(135, 66);
            this.Btn_YJI.TabIndex = 4;
            this.Btn_YJI.Text = "Yuangeki";
            this.Btn_YJI.UseVisualStyleBackColor = true;
            this.Btn_YJI.Click += new System.EventHandler(this.Btn_YJI_Click);
            // 
            // App_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(543, 662);
            this.Controls.Add(this.Btn_YJI);
            this.Controls.Add(this.Btn_ZER);
            this.Controls.Add(this.Btn_DDR);
            this.Controls.Add(this.Btn_ADX);
            this.Name = "App_Main";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button Btn_ADX;
        private System.Windows.Forms.Button Btn_DDR;
        private System.Windows.Forms.Button Btn_ZER;
        private System.Windows.Forms.Button Btn_YJI;
    }
}